// SPDX-License-Identifier: AGPL-3.0-only
namespace GameTranslator.Core;

/// <summary>Dialogue has priority over area work. Independent cloud requests use separate bounded lanes;
/// CPU models and providers with shared fallbacks remain serialized.</summary>
public sealed class TranslationWorkQueue : IDisposable
{
    private sealed record Request(string Text,CancellationToken Token,TaskCompletionSource<string> Result,TranslationProbe? Probe,bool Dialogue,IReadOnlyList<string>? Batch=null,TaskCompletionSource<IReadOnlyList<string>>? BatchResult=null,long SubmittedAt=0);
    private readonly Queue<Request> high=new(),low=new();
    private readonly object gate=new();
    private readonly SemaphoreSlim available=new(0),areaAvailable=new(0);
    private readonly bool independentAreas;
    private readonly Func<long> now;
    private readonly CancellationTokenSource lifetime=new();
    private readonly ITextTranslator provider,areaProvider;
    private bool disposed;
    public ITextTranslator Dialogue {get;}
    public ITextTranslator Areas {get;}
    private readonly Task worker;
    private readonly Task? areaWorker;
    public TranslationWorkQueue(ITextTranslator provider,Func<long>? clock=null)
    {now=clock??(()=>Environment.TickCount64);this.provider=provider;areaProvider=provider.ForIndependentArea();
     independentAreas=provider.SupportsConcurrentAreas&&!ReferenceEquals(provider,areaProvider);
     Dialogue=new Lane(this,true);Areas=new Lane(this,false);worker=Task.Run(()=>Work(false));
     if(independentAreas)areaWorker=Task.Run(()=>Work(true));}
    private void Wake(bool foreground)=>(independentAreas&&!foreground?areaAvailable:available).Release();
    private Task<string> Submit(string text,bool foreground,CancellationToken token)
    {
        lock(gate)
        {
            if(disposed)return Task.FromCanceled<string>(new CancellationToken(true));
            var queue=foreground?high:low;
            // Entries are already bounded by the per-region/latest candidate policy; enforce a hard bound too.
            if(queue.Count>=(foreground?3:8))return Task.FromException<string>(new TranslatorException(TranslatorStatus.ServiceBusy));
            var done=new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            queue.Enqueue(new(text,token,done,TranslationProbe.Current,foreground,SubmittedAt:now()));Wake(foreground);return done.Task.WaitAsync(token);
        }
    }
    private Task<IReadOnlyList<string>> SubmitMany(IReadOnlyList<string> texts,bool foreground,CancellationToken token){
        lock(gate){if(disposed)return Task.FromCanceled<IReadOnlyList<string>>(new CancellationToken(true));var queue=foreground?high:low;
            if(queue.Count>=(foreground?3:8))return Task.FromException<IReadOnlyList<string>>(new TranslatorException(TranslatorStatus.ServiceBusy));
            var done=new TaskCompletionSource<IReadOnlyList<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
            queue.Enqueue(new("",token,new(TaskCreationOptions.RunContinuationsAsynchronously),TranslationProbe.Current,foreground,texts.ToArray(),done,now()));Wake(foreground);return done.Task.WaitAsync(token);
        }
    }
    private async Task Work(bool areasOnly)
    {
        try
        {
            while(true)
            {
                await (areasOnly?areaAvailable:available).WaitAsync(lifetime.Token).ConfigureAwait(false);
                Request? request;lock(gate)request=areasOnly?(low.Count>0?low.Dequeue():null)
                    :high.Count>0?high.Dequeue():!independentAreas&&low.Count>0?low.Dequeue():null;
                if(request is null)continue;
                if(request.Token.IsCancellationRequested){if(request.BatchResult is {} batch)batch.TrySetCanceled(request.Token);else request.Result.TrySetCanceled(request.Token);continue;}
                using var cancel=CancellationTokenSource.CreateLinkedTokenSource(request.Token,lifetime.Token);
                var started=now();
                TranslationProbe.Current=request.Probe;request.Probe?.NoteQueueWait(started-request.SubmittedAt);
                try{
                    var engine=request.Dialogue?provider:areaProvider;
                    if(request.Batch is {} texts){var result=await engine.TranslateManyAsync(texts,cancel.Token).ConfigureAwait(false);request.Probe?.NoteProvider(now()-started);request.BatchResult!.TrySetResult(result);}
                    else{var result=await engine.TranslateAsync(request.Text,cancel.Token).ConfigureAwait(false);request.Probe?.NoteProvider(now()-started);request.Result.TrySetResult(result);}
                }
                catch(OperationCanceledException){if(request.BatchResult is {} batch)batch.TrySetCanceled(cancel.Token);else request.Result.TrySetCanceled(cancel.Token);}
                catch(Exception error){if(request.BatchResult is {} batch)batch.TrySetException(error);else request.Result.TrySetException(error);}
                finally {TranslationProbe.Current=null;}
            }
        }
        catch(OperationCanceledException) { }
        finally{lock(gate){var pending=areasOnly?low:high;
            foreach(var item in pending){item.Result.TrySetCanceled();item.BatchResult?.TrySetCanceled();}pending.Clear();
            if(!independentAreas){foreach(var item in low){item.Result.TrySetCanceled();item.BatchResult?.TrySetCanceled();}low.Clear();}}}
    }
    public void Dispose(){lock(gate){if(disposed)return;disposed=true;}lifetime.Cancel();}
    private sealed class Lane(TranslationWorkQueue queue,bool foreground):ITextTranslator
    {public int PreferredBatchSize=>foreground?queue.provider.PreferredBatchSize:queue.independentAreas?Math.Clamp(queue.areaProvider.PreferredBatchSize,1,4):1;
     public Task<string> TranslateAsync(string text,CancellationToken token=default)=>queue.Submit(text,foreground,token);public async Task<IReadOnlyList<string>> TranslateManyAsync(IReadOnlyList<string> texts,CancellationToken token=default){var result=new List<string>();int count=Math.Clamp(PreferredBatchSize,1,24);foreach(var batch in texts.Chunk(count))result.AddRange(await queue.SubmitMany(batch,foreground,token).ConfigureAwait(false));return result;}}
}
