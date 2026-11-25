// SPDX-License-Identifier: AGPL-3.0-only
namespace GameTranslator.Core;

/// <summary>Auxiliary areas never share a candidate/queue/display with dialogue.
/// One OCR operation per submitted frame; one in-flight translation and one latest candidate per area.
/// No provider or subscriber is called while holding a state lock.</summary>
public sealed class ReadingAreaSession : IDisposable
{
    private sealed class State(ReadingArea area, ITextReader reader)
    {
        public readonly ReadingArea Area=area;
        public readonly ITextReader Reader=reader;
        public string Candidate="", Sent="", LastResult="";
        public long Since, Seen, ReadAt=long.MinValue/2, Revision;
        public bool Busy, Visible;public long RetryAt;public int Failures;
        public CropRegion? Output;public PanelLayout? Panel,LastPanel;public Frame? PanelFrame;public long LastEmit;
        public readonly Dictionary<string,string> Cache=new(StringComparer.Ordinal);
    }
    private readonly object gate=new();
    private readonly List<State> states;
    private readonly ITextTranslator translator;
    private readonly Func<long> now;
    private readonly Func<string,string?>? reviewed;
    private readonly CancellationTokenSource lifetime=new();
    private int cursor, processing;
    private bool disposed;
    public event Action<AreaCaption>? Caption;
    public event Action<string,string>? Failed;

    public ReadingAreaSession(IEnumerable<ReadingArea> areas, Func<ITextReader> makeReader,
        ITextTranslator translator, Func<long>? clock=null,Func<string,string?>? reviewedLookup=null)
    {
        this.translator=translator;reviewed=reviewedLookup;now=clock??(()=>Environment.TickCount64);
        states=areas.Where(a=>a.Enabled&&a.Kind!=ReadingAreaKind.Dialogue).Take(8)
            .GroupBy(a=>a.Id,StringComparer.Ordinal).Select(g=>new State(g.First().Validate(),makeReader())).ToList();
    }

    public async Task ProcessAsync(Frame frame)
    {
        if(Interlocked.Exchange(ref processing,1)!=0)return;
        try
        {
            State? state=null;
            lock(gate)
            {
                if(disposed||states.Count==0)return;
                for(var n=0;n<states.Count;n++)
                {
                    var possible=states[cursor++%states.Count];
                    if(now()-possible.ReadAt<possible.Area.IntervalMs)continue;
                    possible.ReadAt=now();state=possible;break;
                }
            }
            if(state is null)return;
            var s=state;
            var source=s.Area.Source;
            var crop=frame.Crop((int)(source.Left*frame.Width),(int)(source.Top*frame.Height),
                Math.Max(1,(int)((source.Right-source.Left)*frame.Width)),Math.Max(1,(int)((source.Bottom-source.Top)*frame.Height)));
            if(s.Area.Kind==ReadingAreaKind.Phone&&s.Area.Structured){var phone=SpatialText.FindPhone(crop);if(phone is null){Observe(s,"",null,crop);return;}crop=RegionCropper.Crop(crop,phone); }
            if(s.Area.Kind==ReadingAreaKind.Notification&&s.Area.Structured){var message=SpatialText.FindMessage(crop);if(message is null){Observe(s,"",null,crop);return;}crop=RegionCropper.Crop(crop,message); }
            if(s.Area.Kind==ReadingAreaKind.Notification&&s.Area.Structured){
                var o=s.Area.PresentationOutput();var scale=Math.Min(1,Math.Min((o.Right-o.Left)*frame.Width/crop.Width,(o.Bottom-o.Top)*frame.Height/crop.Height));
                s.Output=new(o.Top,o.Top+crop.Height*scale/frame.Height,o.Right-crop.Width*scale/frame.Width,o.Right);
            }
            var reading=await Task.Run(()=>s.Reader.ReadSpatial(crop),lifetime.Token).ConfigureAwait(false);
            // Paragraph/line boundaries are retained for menus; messages are not mixed into dialogue.
            var panel=s.Area.Structured?SpatialText.Build(crop,reading,s.Area.Kind,includeImage:false):null;
            Observe(s,reading.Text.Trim(),panel,crop);
        }
        catch(OperationCanceledException) { }
        catch(Exception error){if(!lifetime.IsCancellationRequested)Failed?.Invoke("ocr",error.GetType().Name);}
        finally { Volatile.Write(ref processing,0); }
    }

    private void Observe(State s,string text,PanelLayout? panel,Frame frame)
    {
        AreaCaption? clear=null,renewed=null;bool start=false;
        lock(gate)
        {
            if(disposed)return;
            var at=now();
            if(text.Length<2)
            {
                if((s.Visible||s.Candidate.Length>0)&&at-s.Seen>=700){s.Visible=false;s.Sent="";s.Candidate="";s.LastResult="";s.LastPanel=null;s.PanelFrame=null;s.Revision++;clear=Make(s,"",true);}
            }
            else
            {
                s.Seen=at;s.Panel=panel;s.PanelFrame=frame;
                if(!TextMatch.IsSimilar(TextMatch.Normalize(text),TextMatch.Normalize(s.Candidate),.94))
                {s.Candidate=text;s.Since=at;s.RetryAt=0;s.Failures=0;}
                if(at-s.Since>=120&&s.Candidate!=s.Sent&&!s.Busy&&at>=s.RetryAt){s.Busy=true;start=true;}
                else if(s.Area.Kind==ReadingAreaKind.Phone&&s.Visible&&s.Sent==s.Candidate&&s.LastPanel is not null&&at-s.LastEmit>=s.Area.DurationMs/2){s.LastEmit=at;s.Revision++;renewed=Make(s,s.LastResult) with{Panel=s.LastPanel};}
            }
        }
        if(clear is not null)Caption?.Invoke(clear);if(renewed is not null)Caption?.Invoke(renewed);
        if(start)_=Task.Run(()=>Translate(s));
    }

    private async Task Translate(State s)
    {
        string source;long revision;PanelLayout? panel;Frame? frame;
        lock(gate){if(disposed){s.Busy=false;return;}source=s.Candidate;panel=s.Panel;frame=s.PanelFrame;revision=++s.Revision;}
        try
        {
            async Task<string> Piece(string text){
                string? hit;lock(gate)s.Cache.TryGetValue(text,out hit);if(hit is not null)return hit;
                var translated=s.Area.Appearance?.ReviewedOnly==true?reviewed?.Invoke(text)??throw new TranslatorException(TranslatorStatus.EngineFailed):await translator.TranslateAsync(text,lifetime.Token).ConfigureAwait(false);
                lock(gate){if(s.Cache.Count>=128)s.Cache.Clear();s.Cache[text]=translated;}return translated;
            }
            string result;
            if(panel is {Cells.Count:>0}){
                var missing=panel.Cells.Select(c=>c.Source).Distinct(StringComparer.Ordinal).Where(t=>{lock(gate)return !s.Cache.ContainsKey(t);}).ToArray();
                if(missing.Length>0&&s.Area.Appearance?.ReviewedOnly!=true){
                    var values=await translator.TranslateManyAsync(missing,lifetime.Token).ConfigureAwait(false);if(values.Count!=missing.Length)throw new TranslatorException(TranslatorStatus.EngineFailed);
                    lock(gate)for(int i=0;i<missing.Length;i++)if(!string.IsNullOrWhiteSpace(values[i]))s.Cache[missing[i]]=values[i];
                }
                var cells=new List<PanelCell>();
                foreach(var cell in panel.Cells){
                    lifetime.Token.ThrowIfCancellationRequested();
                    lock(gate){if(disposed||s.Candidate!=source)return;}
                    try{if(s.Area.Appearance?.ReviewedOnly==true)cells.Add(cell with{Text=await Piece(cell.Source).ConfigureAwait(false)});else{string? translated;lock(gate)s.Cache.TryGetValue(cell.Source,out translated);cells.Add(cell with{Text=translated??""});}}
                    catch(TranslatorException error){Failed?.Invoke(s.Area.Id,error.GetType().Name);cells.Add(cell);}
                }
                panel=panel with{Cells=cells};if(frame is not null&&panel.Kind is "phone" or "notification")panel=SpatialText.WithImage(frame,panel);result=string.Join("\n",cells.Select(c=>c.Text));
            }else result=await Piece(source).ConfigureAwait(false);
            AreaCaption? caption=null;
            lock(gate)
            {
                if(disposed||s.Revision!=revision||s.Candidate!=source||now()-s.Seen>Math.Max(1500,s.Area.IntervalMs*3))return;
                if(s.Cache.Count>=128)s.Cache.Clear();if(panel is null&&!string.IsNullOrWhiteSpace(result))s.Cache[source]=result;
                var partial=panel is not null&&panel.Cells.Any(c=>string.IsNullOrWhiteSpace(c.Text));
                if(partial&&s.Failures++<2){s.Sent="";s.RetryAt=now()+2500;}else s.Sent=source;
                if(!string.IsNullOrWhiteSpace(result)&&result!=s.LastResult){s.LastResult=result;s.LastPanel=panel;s.LastEmit=now();s.Visible=true;caption=Make(s,result) with{Panel=panel};}
            }
            if(caption is not null)Caption?.Invoke(caption);
        }
        catch(OperationCanceledException) { }
        catch(Exception error){if(!lifetime.IsCancellationRequested)Failed?.Invoke(s.Area.Id,error.GetType().Name);}
        finally {lock(gate)s.Busy=false;}
    }
    private static AreaCaption Make(State s,string text,bool clear=false)=>new(s.Area.Id,s.Revision,text,s.Output??s.Area.PresentationOutput(),
        s.Area.FontSize,s.Area.Background,s.Area.DurationMs,clear,s.Area.Appearance);
    public void Dispose(){lock(gate){if(disposed)return;disposed=true;}lifetime.Cancel();}
}
