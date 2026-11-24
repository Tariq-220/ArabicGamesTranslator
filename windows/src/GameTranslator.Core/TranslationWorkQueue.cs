// SPDX-License-Identifier: AGPL-3.0-only
namespace GameTranslator.Core;

/// <summary>A shared single-provider lane. Dialogue has priority over menu/phone work;
/// independent region readers cannot flood a CPU model or collide with its inference lock.</summary>
public sealed class TranslationWorkQueue : IDisposable
{
    private sealed record Request(string Text,CancellationToken Token,TaskCompletionSource<string> Result,TranslationProbe? Probe,bool Dialogue);
    private readonly Queue<Request> high=new(),low=new();
    private readonly object gate=new();
    private readonly SemaphoreSlim available=new(0);
    private readonly CancellationTokenSource lifetime=new();
    private readonly ITextTranslator provider,areaProvider;
    private bool disposed;
    public ITextTranslator Dialogue {get;}
    public ITextTranslator Areas {get;}
    private readonly Task worker;
    public TranslationWorkQueue(ITextTranslator provider)
    {this.provider=provider;areaProvider=provider.ForIndependentArea();Dialogue=new Lane(this,true);Areas=new Lane(this,false);worker=Task.Run(Work);}
    private Task<string> Submit(string text,bool foreground,CancellationToken token)
    {
        lock(gate)
        {
            if(disposed)return Task.FromCanceled<string>(new CancellationToken(true));
            var queue=foreground?high:low;
            // Entries are already bounded by the per-region/latest candidate policy; enforce a hard bound too.
            if(queue.Count>=(foreground?3:8))return Task.FromException<string>(new TranslatorException(TranslatorStatus.EngineFailed));
            var done=new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            queue.Enqueue(new(text,token,done,TranslationProbe.Current,foreground));available.Release();return done.Task.WaitAsync(token);
        }
    }
    private async Task Work()
    {
        try
        {
            while(true)
            {
                await available.WaitAsync(lifetime.Token).ConfigureAwait(false);
                Request? request;lock(gate)request=high.Count>0?high.Dequeue():low.Count>0?low.Dequeue():null;
                if(request is null)continue;
                if(request.Token.IsCancellationRequested){request.Result.TrySetCanceled(request.Token);continue;}
                using var cancel=CancellationTokenSource.CreateLinkedTokenSource(request.Token,lifetime.Token);
                TranslationProbe.Current=request.Probe;
                try{request.Result.TrySetResult(await (request.Dialogue?provider:areaProvider).TranslateAsync(request.Text,cancel.Token).ConfigureAwait(false));}
                catch(OperationCanceledException){request.Result.TrySetCanceled(cancel.Token);}
                catch(Exception error){request.Result.TrySetException(error);}
                finally {TranslationProbe.Current=null;}
            }
        }
        catch(OperationCanceledException) { }
        finally{lock(gate){foreach(var item in high.Concat(low))item.Result.TrySetCanceled();high.Clear();low.Clear();}}
    }
    public void Dispose(){lock(gate){if(disposed)return;disposed=true;}lifetime.Cancel();}
    private sealed class Lane(TranslationWorkQueue queue,bool foreground):ITextTranslator
    {public Task<string> TranslateAsync(string text,CancellationToken token=default)=>queue.Submit(text,foreground,token);}
}
