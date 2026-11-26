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
        public readonly PhonePanelTracker Phone=new();public int FullWidth,FullHeight;public TextBox? PhoneBounds;public long PhoneVerifiedAt=long.MinValue/2;
        public string Candidate="", Sent="", LastResult="";
        public long Since, Seen, ReadAt=long.MinValue/2, Revision, Generation;
        public int Misses;public long MissingSince;public bool Busy, Visible;public long RetryAt;public int Failures;
        public CropRegion? Output;public PanelLayout? Panel,LastPanel;public Frame? PanelFrame;public long LastEmit;
        public readonly Dictionary<string,string> Cache=new(StringComparer.Ordinal);
    }
    private readonly object gate=new();
    private readonly List<State> states;
    private readonly ITextTranslator translator;
    private readonly Func<long> now;
    private readonly Func<string,string?>? reviewed;
    private readonly Func<string,TextBox,bool>? dialogue;
    private readonly CancellationTokenSource lifetime=new();
    private int cursor, processing;
    private bool disposed;
    private ReadingTiming timing=new();
    public void SetTiming(ReadingTiming value){lock(gate)timing=value.Validate();}
    public event Action<AreaCaption>? Caption;
    public event Action<string,string>? Failed;
    public event Action<string,string>? Status;
    public event Action<AreaTrace>? Trace;
    private static string Key(string text)=>TextMatch.Normalize(text)+(text.Contains('?')?"?":"");

    public ReadingAreaSession(IEnumerable<ReadingArea> areas, Func<ITextReader> makeReader,
        ITextTranslator translator, Func<long>? clock=null,Func<string,string?>? reviewedLookup=null,Func<string,TextBox,bool>? isDialogue=null)
    {
        this.translator=translator;reviewed=reviewedLookup;dialogue=isDialogue;now=clock??(()=>Environment.TickCount64);
        states=ReadingArea.Unique(areas).Where(a=>a.Enabled&&a.Kind!=ReadingAreaKind.Dialogue).Take(8)
            .GroupBy(a=>a.Id,StringComparer.Ordinal).Select(g=>new State(g.First().Validate(),makeReader())).ToList();
    }

    public Task ProcessAsync(Frame frame)
    {
        if(Interlocked.Exchange(ref processing,1)!=0)return Task.CompletedTask;
        return Task.Run(()=>ProcessFrame(frame));
    }
    private void ProcessFrame(Frame frame)
    {
        try
        {
            State? state=null;
            lock(gate)
            {
                if(disposed||states.Count==0)return;
                for(var n=0;n<states.Count;n++)
                {
                    var possible=states[cursor++%states.Count];
                    if(now()-possible.ReadAt<(timing.AreaReadMs??possible.Area.IntervalMs))continue;
                    possible.ReadAt=now();state=possible;break;
                }
            }
            if(state is null)return;
            var s=state;s.FullWidth=frame.Width;s.FullHeight=frame.Height;
            var source=s.Area.Source;var actualSource=new TextBox(source.Left,source.Top,source.Right,source.Bottom);
            var crop=frame.Crop((int)(source.Left*frame.Width),(int)(source.Top*frame.Height),
                Math.Max(1,(int)((source.Right-source.Left)*frame.Width)),Math.Max(1,(int)((source.Bottom-source.Top)*frame.Height)));
            if(s.Area.Kind==ReadingAreaKind.Phone&&s.Area.Structured&&s.Area.AutoDetect){var detected=SpatialText.FindPhone(crop);var phone=s.Phone.Locate(detected,now());if(phone is null||detected is null&&!SpatialText.PhoneSurfacePresent(crop,phone)){Status?.Invoke(s.Area.Id,"لم تُكتشف حدود الجوال؛ حدّد مربع الشاشة ثم أوقف الكشف التلقائي عند الحاجة.");Observe(s,"",null,crop);return;}actualSource=new TextBox(phone.Left,phone.Top,phone.Right,phone.Bottom).PlacedIn(actualSource);crop=RegionCropper.Crop(crop,phone); }
            if(s.Area.Kind==ReadingAreaKind.Notification&&s.Area.Structured&&s.Area.AutoDetect){var message=SpatialText.FindMessage(crop);if(message is null){Status?.Invoke(s.Area.Id,"لم تُكتشف بطاقة التنبيه؛ راجع مربع القراءة أو استخدم حدوده مباشرة.");Observe(s,"",null,crop);return;}actualSource=new TextBox(message.Left,message.Top,message.Right,message.Bottom).PlacedIn(actualSource);crop=RegionCropper.Crop(crop,message); }
            lifetime.Token.ThrowIfCancellationRequested();var reading=s.Area.Kind==ReadingAreaKind.Instructions?s.Reader.ReadLines(crop,true):s.Reader.ReadSpatial(crop);if(s.Area.Kind==ReadingAreaKind.Menu&&s.Area.AutoDetect)reading=MenuReading.Select(crop,reading,!states.Any(x=>x.Area.Kind==ReadingAreaKind.MapCard));
            if(s.Area.Kind is not (ReadingAreaKind.Phone or ReadingAreaKind.Notification)){
                var originalCount=reading.Lines.Count;reading=WithoutMessageText(reading,actualSource);
                if(originalCount>0&&reading.Lines.Count==0){ClearConflicting(s);return;}
            }
            if(s.Area.Kind==ReadingAreaKind.Phone&&s.Area.Structured&&s.Area.AutoDetect&&!SpatialText.IsConversation(reading)){Status?.Invoke(s.Area.Id,"المنطقة لا تحتوي محادثة جوال مؤكدة؛ لن يُعرض قالب فارغ فوق الخريطة.");Observe(s,"",null,crop);return;}
            if(s.Area.Kind==ReadingAreaKind.Instructions&&dialogue is not null){
                var originalCount=reading.Lines.Count;var bounds=new TextBox(source.Left,source.Top,source.Right,source.Bottom);
                reading=new Reading(reading.Lines.Where(l=>l.Box is not {} b||!dialogue(l.Text,b.PlacedIn(bounds))));
                if(originalCount>0&&reading.Lines.Count==0){ClearConflicting(s);return;}
            }
            // Paragraph/line boundaries are retained for menus; messages are not mixed into dialogue.
            if(s.Area.Kind==ReadingAreaKind.MapCard)reading=new Reading(reading.Lines.Where(l=>l.Box is {} b&&SpatialText.HasDarkBackground(crop,b)));
            var panel=s.Area.Structured?SpatialText.Build(crop,reading,s.Area.Kind,includeImage:false):null;
            if(s.Area.Kind is ReadingAreaKind.Phone or ReadingAreaKind.Notification&&panel is not null){panel=s.Phone.Observe(panel,now());if(panel is null){Status?.Invoke(s.Area.Id,"بانتظار نص محادثة كامل؛ لن تُعرض لوحة باسم فقط.");Observe(s,"",null,crop);return;}}
            if(s.Area.Kind==ReadingAreaKind.Notification&&panel is not null){
                var content=Key(string.Join(" ",panel.Cells.Select(c=>c.Source)));bool duplicate;
                lock(gate)duplicate=states.TakeWhile(other=>other!=s).Any(other=>other.Area.Kind==ReadingAreaKind.Notification&&other.PhoneBounds is {} b&&now()-other.PhoneVerifiedAt<=1800&&other.Panel is {} existing&&Key(string.Join(" ",existing.Cells.Select(c=>c.Source)))==content&&Intersection(actualSource,b)>.65);
                if(duplicate){ClearConflicting(s);Status?.Invoke(s.Area.Id,"التنبيه نفسه تقرؤه بطاقة أخرى؛ أُوقف العرض المكرر.");return;}
            }
            if(s.Area.Kind is ReadingAreaKind.Phone or ReadingAreaKind.Notification&&panel is not null){lock(gate){s.PhoneBounds=actualSource;s.PhoneVerifiedAt=now();}}
            Status?.Invoke(s.Area.Id,string.IsNullOrWhiteSpace(reading.Text)?"اللوحة موجودة، لكن OCR لم يقرأ نصًا.":$"قُرئت {reading.Lines.Count} أسطر · الترجمة قيد المعالجة");
            Observe(s,panel is {Cells.Count:>0}?string.Join("\n",panel.Cells.Select(c=>c.Source)):reading.Text.Trim(),panel is {Cells.Count:0}?null:panel,crop);
        }
        catch(OperationCanceledException) { }
        catch(Exception error){if(!lifetime.IsCancellationRequested)Failed?.Invoke("ocr",error.GetType().Name);}
        finally { Volatile.Write(ref processing,0); }
    }

    private static double Intersection(TextBox a,TextBox b){var overlap=Math.Max(0,Math.Min(a.Right,b.Right)-Math.Max(a.Left,b.Left))*Math.Max(0,Math.Min(a.Bottom,b.Bottom)-Math.Max(a.Top,b.Top));var smaller=Math.Max(.000001,Math.Min((a.Right-a.Left)*(a.Bottom-a.Top),(b.Right-b.Left)*(b.Bottom-b.Top)));return overlap/smaller;}
    public Reading WithoutPhoneText(Reading input,TextBox dialogueArea)=>WithoutMessageText(input,dialogueArea);
    public Reading WithoutMessageText(Reading input,TextBox dialogueArea)
    {
        lock(gate){
            var phones=states.Where(s=>s.Area.Kind is ReadingAreaKind.Phone or ReadingAreaKind.Notification&&s.PhoneBounds is {} bounds&&(bounds.Right-bounds.Left)*(bounds.Bottom-bounds.Top)<.55&&now()-s.PhoneVerifiedAt<=1800&&s.Panel is not null).ToArray();
            if(phones.Length==0)return input;
            return new Reading(input.Lines.Where(line=>{
                if(line.Box is not {} local)return true;var box=local.PlacedIn(dialogueArea);var text=TextMatch.Normalize(line.Text);
                if(text.Length<5)return true;
                foreach(var phone in phones){var b=phone.PhoneBounds!.Value;double overlap=Math.Max(0,Math.Min(b.Right,box.Right)-Math.Max(b.Left,box.Left))*Math.Max(0,Math.Min(b.Bottom,box.Bottom)-Math.Max(b.Top,box.Top));double area=Math.Max(.000001,(box.Right-box.Left)*(box.Bottom-box.Top));
                    if(overlap/area<.8)continue;
                    if(phone.Panel!.Cells.Any(c=>{var cell=TextMatch.Normalize(c.Source);return cell.Contains(text,StringComparison.Ordinal)||TextMatch.IsSimilar(cell,text,.94);}))return false;
                }
                return true;
            }));
        }
    }
    private void ClearConflicting(State s){AreaCaption? clear=null;lock(gate){if(s.Visible||s.Candidate.Length>0){s.Visible=false;s.Candidate=s.Sent=s.LastResult="";s.LastPanel=null;s.Generation++;s.Revision++;clear=Make(s,"",true);}}Status?.Invoke(s.Area.Id,"نص الحوار مستبعد من بطاقة المهام لمنع التكرار والإطار الملون.");if(clear is not null)Caption?.Invoke(clear);}

    private void Observe(State s,string text,PanelLayout? panel,Frame frame)
    {
        AreaCaption? clear=null,renewed=null;bool start=false;
        lock(gate)
        {
            if(disposed)return;
            var at=now();
            if(text.Length<2)
            {
                if(s.Misses==0)s.MissingSince=at;
                if((s.Visible||s.Candidate.Length>0)&&++s.Misses>=2&&at-s.MissingSince>=timing.GoneMs){s.Visible=false;s.Sent="";s.Candidate="";s.LastResult="";s.LastPanel=null;s.PanelFrame=null;s.Phone.Reset();s.Generation++;s.Revision++;clear=Make(s,"",true);}
            }
            else
            {
                s.Misses=0;s.Seen=at;s.Panel=panel;s.PanelFrame=frame;
                if(!TextMatch.IsSimilar(TextMatch.Normalize(text),TextMatch.Normalize(s.Candidate),.94))
                {s.Candidate=text;s.Since=at;s.RetryAt=0;s.Failures=0;}
                var cellsChanged=panel is not null&&s.LastPanel is not null&&!panel.Cells.Select(c=>c.Source).SequenceEqual(s.LastPanel.Cells.Select(c=>c.Source));
                if((at-s.Since>=timing.SettleMs||(panel is {Cells.Count:>0}?panel.Cells.All(c=>s.Cache.ContainsKey(Key(c.Source))):s.Cache.ContainsKey(Key(text))))&&(s.Candidate!=s.Sent||cellsChanged)&&!s.Busy&&at>=s.RetryAt){s.Busy=true;start=true;}
                else if(s.Area.Kind!=ReadingAreaKind.Notification&&s.Visible&&s.Sent==s.Candidate&&s.LastResult.Length>0&&at-s.LastEmit>=s.Area.DurationMs/2){s.LastEmit=at;s.Revision++;renewed=Make(s,s.LastResult) with{Panel=s.LastPanel};}
            }
        }
        if(clear is not null){Trace?.Invoke(new(s.Area.Id,s.Area.Name,"[source gone]","","cleared"));Caption?.Invoke(clear);}if(renewed is not null)Caption?.Invoke(renewed);
        if(start)_=Task.Run(()=>Translate(s));
    }

    private void PublishCached(State s,long generation,bool final,string originalSource)
    {
        AreaCaption? caption=null;AreaTrace? trace=null;
        lock(gate)
        {
            if(disposed||s.Generation!=generation||s.Candidate.Length==0)return;
            var panel=s.Panel;var frame=s.PanelFrame;
            var result=panel is {Cells.Count:>0}?string.Join("\n",panel.Cells.Select(c=>s.Cache.GetValueOrDefault(Key(c.Source),""))):s.Cache.GetValueOrDefault(Key(s.Candidate),"");
            if(panel is not null){panel=panel with{Cells=panel.Cells.Select(c=>c with{Text=s.Cache.GetValueOrDefault(Key(c.Source),"")}).ToArray()};}
            var partial=panel is not null&&panel.Cells.Any(c=>string.IsNullOrWhiteSpace(c.Text));
            if(final){
                if(s.Candidate!=originalSource){s.Sent="";s.RetryAt=0;}
                else if(partial||string.IsNullOrWhiteSpace(result)){s.Sent="";s.Failures++;s.RetryAt=now()+Math.Min(8000,1000*(1<<Math.Min(3,s.Failures-1)));}
                else s.Sent=s.Candidate;
            }
            if(!string.IsNullOrWhiteSpace(result)&&(result!=s.LastResult||panel is not null&&s.LastPanel is not null&&!panel.Cells.Select(c=>Key(c.Source)).SequenceEqual(s.LastPanel.Cells.Select(c=>Key(c.Source))))){
                if(panel is {Kind:"notification"}){
                    if(panel.Cells.Any(c=>string.IsNullOrWhiteSpace(c.Text)))return;
                    var o=s.Area.PresentationOutput();var scale=Math.Min(1,Math.Min((o.Right-o.Left)*s.FullWidth/panel.Width,(o.Bottom-o.Top)*s.FullHeight/panel.Height));
                    s.Output=new(o.Top,o.Top+panel.Height*scale/s.FullHeight,o.Right-panel.Width*scale/s.FullWidth,o.Right);
                    if(frame is not null)panel=SpatialText.WithImage(frame,panel);
                }
                if(panel is {Kind:"phone"}){
                    var body=panel.Cells.Where(PhonePanelTracker.Body).ToArray();if(body.Length==0||body.Any(c=>string.IsNullOrWhiteSpace(c.Text)))return;
                    var o=s.Output??s.Area.PresentationOutput();double scale=Math.Max(.01,Math.Min((o.Right-o.Left)*s.FullWidth/panel.Width,(o.Bottom-o.Top)*s.FullHeight/panel.Height));
                    panel=SpatialText.ReflowPhone(panel,s.Area.Appearance?.MatchSize==false?s.Area.FontSize*s.FullWidth/1920d/scale:null);
                }
                s.LastResult=result;s.LastPanel=panel;s.LastEmit=now();s.Visible=true;s.Revision++;
                caption=Make(s,result) with{Panel=panel};
                trace=new(s.Area.Id,s.Area.Name,s.Candidate,result,partial?"partial":"shown");
            }
        }
        if(caption is not null){Status?.Invoke(s.Area.Id,$"تُعرض الترجمة · {caption.Panel?.Cells.Count(c=>!string.IsNullOrWhiteSpace(c.Text))??1} أجزاء");Caption?.Invoke(caption);}
        if(trace is not null)Trace?.Invoke(trace);
    }

    private async Task Translate(State s)
    {
        string source;long generation;PanelLayout? panel;long started=now();
        lock(gate){if(disposed){s.Busy=false;return;}source=s.Candidate;panel=s.Panel;generation=s.Generation;}
        Trace?.Invoke(new(s.Area.Id,s.Area.Name,source,"","translating"));
        try
        {
            var pieces=panel is {Cells.Count:>0}?panel.Cells.Select(c=>c.Source).DistinctBy(Key).ToArray():new[]{source};
            foreach(var piece in pieces){if(reviewed?.Invoke(piece) is {Length:>0} ready){lock(gate)s.Cache[Key(piece)]=ready;}}
            var missing=pieces.Where(t=>{lock(gate)return !s.Cache.ContainsKey(Key(t));}).ToArray();
            // First show reusable cells. Each small batch is published immediately; a slow last message
            // or a rejected English cell must not hide the rest of the phone conversation.
            PublishCached(s,generation,false,source);
            int batchSize=Math.Clamp(translator.PreferredBatchSize,1,4);
            foreach(var pendingBatch in missing.Chunk(batchSize))
            {
                lifetime.Token.ThrowIfCancellationRequested();
                string[] batch;
                lock(gate){
                    if(disposed||s.Generation!=generation)return;
                    // Do not spend the provider on messages which were scrolled out while an earlier item ran.
                    var wanted=s.Panel is {Cells.Count:>0}?s.Panel.Cells.Select(c=>Key(c.Source)).ToHashSet(StringComparer.Ordinal):new HashSet<string>([Key(s.Candidate)],StringComparer.Ordinal);
                    batch=pendingBatch.Where(t=>wanted.Contains(Key(t))).ToArray();
                }
                if(batch.Length==0)continue;
                IReadOnlyList<string> values;
                if(s.Area.Appearance?.ReviewedOnly==true)values=batch.Select(t=>reviewed?.Invoke(t)??"").ToArray();
                else values=await translator.TranslateManyAsync(batch,lifetime.Token).ConfigureAwait(false);
                if(values.Count!=batch.Length)throw new TranslatorException(TranslatorStatus.EngineFailed);
                for(int i=0;i<batch.Length;i++){
                    var value=values[i];
                    if(!string.IsNullOrWhiteSpace(value)){lock(gate){if(s.Cache.Count>=128)s.Cache.Clear();s.Cache[Key(batch[i])]=value;}}
                    Trace?.Invoke(new(s.Area.Id,s.Area.Name,batch[i],value,string.IsNullOrWhiteSpace(value)?"rejected_output":"translated",(int)Math.Clamp(now()-started,0,int.MaxValue)));
                }
                PublishCached(s,generation,false,source);
            }
            PublishCached(s,generation,true,source);
        }
        catch(OperationCanceledException) { }
        catch(Exception error){if(!lifetime.IsCancellationRequested){var reason=error is TranslatorException failure?failure.Status.ToString():error.GetType().Name;Status?.Invoke(s.Area.Id,"تعذر طلب الترجمة: "+reason);Trace?.Invoke(new(s.Area.Id,s.Area.Name,source,"","failed",(int)Math.Clamp(now()-started,0,int.MaxValue),reason));Failed?.Invoke(s.Area.Id,reason);lock(gate)s.RetryAt=now()+1000;}}
        finally {lock(gate)s.Busy=false;}
    }
    private static AreaCaption Make(State s,string text,bool clear=false)=>new(s.Area.Id,s.Revision,text,s.Output??s.Area.PresentationOutput(),
        s.Area.FontSize,s.Area.Background,s.Area.DurationMs,clear,s.Area.Appearance);
    public void Dispose(){lock(gate){if(disposed)return;disposed=true;}lifetime.Cancel();}
}

public sealed record AreaTrace(string AreaId,string AreaName,string Source,string Translation,string Stage,int ElapsedMs=0,string? Error=null);
