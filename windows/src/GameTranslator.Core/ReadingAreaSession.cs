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
        public bool Busy, Visible;
        public PanelLayout? Panel;
        public readonly Dictionary<string,string> Cache=new(StringComparer.Ordinal);
    }
    private readonly object gate=new();
    private readonly List<State> states;
    private readonly ITextTranslator translator;
    private readonly Func<long> now;
    private readonly CancellationTokenSource lifetime=new();
    private int cursor, processing;
    private bool disposed;
    public event Action<AreaCaption>? Caption;
    public event Action<string,string>? Failed;

    public ReadingAreaSession(IEnumerable<ReadingArea> areas, Func<ITextReader> makeReader,
        ITextTranslator translator, Func<long>? clock=null)
    {
        this.translator=translator;now=clock??(()=>Environment.TickCount64);
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
            if(s.Area.Kind==ReadingAreaKind.Phone&&SpatialText.FindPhone(crop) is {} phone)crop=RegionCropper.Crop(crop,phone);
            var reading=await Task.Run(()=>s.Reader.ReadSpatial(crop),lifetime.Token).ConfigureAwait(false);
            // Paragraph/line boundaries are retained for menus; messages are not mixed into dialogue.
            var panel=s.Area.Structured?SpatialText.Build(crop,reading,s.Area.Kind):null;
            Observe(s,reading.Text.Trim(),panel);
        }
        catch(OperationCanceledException) { }
        catch(Exception error){if(!lifetime.IsCancellationRequested)Failed?.Invoke("ocr",error.GetType().Name);}
        finally { Volatile.Write(ref processing,0); }
    }

    private void Observe(State s,string text,PanelLayout? panel)
    {
        AreaCaption? clear=null;bool start=false;
        lock(gate)
        {
            if(disposed)return;
            var at=now();
            if(text.Length<2)
            {
                if(s.Visible&&at-s.Seen>=700){s.Visible=false;s.Sent="";s.Candidate="";s.Revision++;clear=Make(s,"",true);}
            }
            else
            {
                s.Seen=at;s.Panel=panel;
                if(!TextMatch.IsSimilar(TextMatch.Normalize(text),TextMatch.Normalize(s.Candidate),.94))
                {s.Candidate=text;s.Since=at;}
                if(at-s.Since>=120&&s.Candidate!=s.Sent&&!s.Busy){s.Busy=true;start=true;}
            }
        }
        if(clear is not null)Caption?.Invoke(clear);
        if(start)_=Task.Run(()=>Translate(s));
    }

    private async Task Translate(State s)
    {
        string source;long revision;PanelLayout? panel;
        lock(gate){if(disposed){s.Busy=false;return;}source=s.Candidate;panel=s.Panel;revision=++s.Revision;}
        try
        {
            async Task<string> Piece(string text){
                string? hit;lock(gate)s.Cache.TryGetValue(text,out hit);if(hit is not null)return hit;
                var translated=await translator.TranslateAsync(text,lifetime.Token).ConfigureAwait(false);
                lock(gate){if(s.Cache.Count>=128)s.Cache.Clear();s.Cache[text]=translated;}return translated;
            }
            string result;
            if(panel is {Cells.Count:>0}){
                var cells=new List<PanelCell>();
                foreach(var cell in panel.Cells){
                    lifetime.Token.ThrowIfCancellationRequested();
                    lock(gate){if(disposed||s.Candidate!=source)return;}
                    try{cells.Add(cell with{Text=await Piece(cell.Source).ConfigureAwait(false)});}
                    catch(TranslatorException error){Failed?.Invoke(s.Area.Id,error.GetType().Name);cells.Add(cell);}
                }
                panel=panel with{Cells=cells};result=string.Join("\n",cells.Select(c=>c.Text));
            }else result=await Piece(source).ConfigureAwait(false);
            AreaCaption? caption=null;
            lock(gate)
            {
                if(disposed||s.Revision!=revision||s.Candidate!=source||now()-s.Seen>1000)return;
                if(s.Cache.Count>=128)s.Cache.Clear();s.Cache[source]=result;
                s.Sent=source;s.LastResult=result;s.Visible=true;caption=Make(s,result) with{Panel=panel};
            }
            Caption?.Invoke(caption);
        }
        catch(OperationCanceledException) { }
        catch(Exception error){if(!lifetime.IsCancellationRequested)Failed?.Invoke(s.Area.Id,error.GetType().Name);}
        finally {lock(gate)s.Busy=false;}
    }
    private static AreaCaption Make(State s,string text,bool clear=false)=>new(s.Area.Id,s.Revision,text,s.Area.Output,
        s.Area.FontSize,s.Area.Background,s.Area.DurationMs,clear,s.Area.Appearance);
    public void Dispose(){lock(gate){if(disposed)return;disposed=true;}lifetime.Cancel();}
}
