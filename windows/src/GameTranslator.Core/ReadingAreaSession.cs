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
        public readonly PhonePanelTracker Phone=new();public int FullWidth,FullHeight;public TextBox? PhoneBounds;public PanelLayout? OwnedMessages;public long PhoneVerifiedAt=long.MinValue/2;
        public string Candidate="", Sent="", LastResult="",TranslationError="";
        public Reading SourceReading=Reading.Empty;
        public long Since, Seen, ReadAt=long.MinValue/2, Revision, Generation;
        public Frame? CachedFrame;public Reading CachedReading=Reading.Empty;public long CachedAt;
        public CancellationTokenSource? ActiveTranslation;
        public int Misses;public long MissingSince;public bool Busy, Visible;public long RetryAt;public int Failures;
        public CropRegion? Output;public PanelLayout? Panel,LastPanel,LastSourcePanel;public Frame? PanelFrame;public long LastEmit;public string LastRejected="";
        public readonly Dictionary<string,string> Cache=new(StringComparer.Ordinal);
    }
    private readonly object gate=new();
    private readonly List<State> states;
    private readonly ITextTranslator translator;
    private readonly Func<ITextReader> makeReader;
    private readonly Func<long> now;
    private readonly Func<string,string?>? reviewed;
    private readonly Func<string,TextBox,bool>? dialogue;
    private readonly CancellationTokenSource lifetime=new();
    private int cursor, processing;
    private bool disposed;
    private ReadingTiming timing=new();
    /// <summary>Windows pixel-deterministic OCR: reuse only byte-identical crops, refresh every second.</summary>
    public bool ReuseIdenticalFrames {get;set;}
    public void SetTiming(ReadingTiming value){lock(gate)timing=value.Validate();}
    public event Action<AreaCaption>? Caption;
    public event Action<string,string>? Failed;
    public event Action<string,string>? Status;
    public event Action<AreaTrace>? Trace;
    private static string Key(string text)=>TextMatch.Normalize(text)+(text.Contains('?')?"?":"");

    public ReadingAreaSession(IEnumerable<ReadingArea> areas, Func<ITextReader> makeReader,
        ITextTranslator translator, Func<long>? clock=null,Func<string,string?>? reviewedLookup=null,Func<string,TextBox,bool>? isDialogue=null)
    {
        this.translator=translator;this.makeReader=makeReader;reviewed=reviewedLookup;dialogue=isDialogue;now=clock??(()=>Environment.TickCount64);
        states=ReadingArea.Unique(areas).Where(a=>a.Enabled&&a.Kind!=ReadingAreaKind.Dialogue).Take(8)
            .GroupBy(a=>a.Id,StringComparer.Ordinal).Select(g=>new State(g.First().Validate(),makeReader())).ToList();
    }

    public void UpdateAreas(IEnumerable<ReadingArea> areas)
    {
        var next=ReadingArea.Unique(areas).Where(a=>a.Enabled&&a.Kind!=ReadingAreaKind.Dialogue).Take(8).GroupBy(a=>a.Id,StringComparer.Ordinal).Select(g=>g.First().Validate()).ToArray();
        var removed=new List<State>();
        lock(gate){if(disposed)return;var updated=new List<State>();foreach(var area in next){var previous=states.FirstOrDefault(s=>s.Area.Id==area.Id);if(previous is not null&&previous.Area==area)updated.Add(previous);else{var state=new State(area,previous?.Reader??makeReader()){Revision=previous is null?0:previous.Revision+1};if(previous is not null)foreach(var pair in previous.Cache)state.Cache[pair.Key]=pair.Value;updated.Add(state);}}removed=states.Except(updated).ToList();states.Clear();states.AddRange(updated);cursor=0;}
        foreach(var old in removed)ClearConflicting(old);
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
            lifetime.Token.ThrowIfCancellationRequested();
            var unchanged=ReuseIdenticalFrames&&s.CachedFrame is {} previous&&now()-s.CachedAt<1000&&previous.Width==crop.Width&&previous.Height==crop.Height&&previous.Stride==crop.Stride&&previous.Pixels.AsSpan().SequenceEqual(crop.Pixels);
            var reading=unchanged?s.CachedReading:s.Area.Kind==ReadingAreaKind.Instructions?s.Reader.ReadLines(crop,true):s.Reader.ReadSpatial(crop);
            lock(gate){if(disposed||!states.Contains(s))return;}
            if(ReuseIdenticalFrames&&!unchanged){s.CachedReading=reading;s.CachedAt=now();s.CachedFrame=new Frame((byte[])crop.Pixels.Clone(),crop.Width,crop.Height,crop.Stride);}
            if(s.Area.Kind==ReadingAreaKind.Menu&&s.Area.AutoDetect)reading=MenuReading.Select(crop,reading,!states.Any(x=>x.Area.Kind==ReadingAreaKind.MapCard));
            if(s.Area.Kind is not (ReadingAreaKind.Phone or ReadingAreaKind.Notification)){
                var originalCount=reading.Lines.Count;reading=WithoutMessageText(reading,actualSource,false);
                if(originalCount>0&&reading.Lines.Count==0){ClearConflicting(s);return;}
            }
            if(s.Area.Kind==ReadingAreaKind.Instructions&&dialogue is not null){
                var originalCount=reading.Lines.Count;var bounds=new TextBox(source.Left,source.Top,source.Right,source.Bottom);
                reading=new Reading(reading.Lines.Where(l=>l.Box is not {} b||!dialogue(l.Text,b.PlacedIn(bounds))));
                if(originalCount>0&&reading.Lines.Count==0){ClearConflicting(s);return;}
            }
            // Paragraph/line boundaries are retained for menus; messages are not mixed into dialogue.
            if(s.Area.Kind==ReadingAreaKind.MapCard)reading=new Reading(reading.Lines.Where(l=>l.Box is {} b&&SpatialText.HasDarkBackground(crop,b)));
            reading=AuxiliaryReadingEvidence.Select(reading,s.Area.Kind);
            if(reading.Lines.Count>0)lock(gate)s.SourceReading=reading;
            var messageArea=s.Area.Kind is ReadingAreaKind.Phone or ReadingAreaKind.Notification;
            var originalMessage=reading;var wrongSource=messageArea&&PhoneReadingEvidence.Incompatible(reading);
            if(s.Area.Kind==ReadingAreaKind.Phone)reading=PhoneReadingEvidence.WithoutGameplay(reading);
            var panel=s.Area.Structured||messageArea?SpatialText.Build(crop,reading,s.Area.Kind,includeImage:false):null;
            var phoneAccepted=s.Area.Kind!=ReadingAreaKind.Phone||PhoneReadingEvidence.Accepts(crop,reading,source,s.Area.Structured&&s.Area.AutoDetect,panel is not null&&s.Phone.MatchesCurrentPage(panel));
            if(messageArea&&(PhoneReadingEvidence.Incompatible(reading)||wrongSource&&!phoneAccepted||s.Area.Kind==ReadingAreaKind.Notification&&wrongSource)){
                var rejected=Key(originalMessage.Text);var log=s.LastRejected!=rejected;ClearConflicting(s);s.LastRejected=rejected;
                Status?.Invoke(s.Area.Id,"هذا حوار أو نص خريطة، وليس رسالة جوال؛ بقي متاحًا لقناته الأصلية.");
                if(log)Trace?.Invoke(new(s.Area.Id,s.Area.Name,originalMessage.Text,"","ignored_wrong_content"));return;
            }
            if(!phoneAccepted){Status?.Invoke(s.Area.Id,"لم تتأكد محادثة الجوال؛ اختر منطقة شاشة الجوال أو بطاقة جوال السيارة.");Observe(s,"",null,crop);return;}
            if(messageArea){
                if(panel is not {Cells.Count:>0}){Status?.Invoke(s.Area.Id,"قُرئ نص بلا مواضع رسائل؛ بانتظار قراءة مكانية، دون تحويله إلى مربع حوار.");Observe(s,"",null,crop);return;}
                // Claim the measured cells before page stabilization or translation. Different OCR
                // spellings of this same physical message must never become another area's caption.
                lock(gate){s.PhoneBounds=actualSource;s.OwnedMessages=panel;s.PhoneVerifiedAt=now();s.LastRejected="";}
                ReleaseConflictingOwners();
            }
            if(s.Area.Kind is ReadingAreaKind.Phone or ReadingAreaKind.Notification&&panel is not null){panel=s.Phone.Observe(panel,now());if(panel is null){Status?.Invoke(s.Area.Id,"بانتظار نص محادثة كامل؛ لن تُعرض لوحة باسم فقط.");Observe(s,"",null,crop);return;}}
            if(messageArea&&panel is not null&&SuppressDuplicate(s,panel,actualSource))return;
            Observe(s,panel is {Cells.Count:>0}?string.Join("\n",panel.Cells.Select(c=>c.Source)):reading.Text.Trim(),!s.Area.Structured||panel is {Cells.Count:0}?null:panel,crop);
            ReportReadState(s,reading.Lines.Count);
        }
        catch(OperationCanceledException) { }
        catch(Exception error){if(!lifetime.IsCancellationRequested)Failed?.Invoke("ocr",error.GetType().Name);}
        finally { Volatile.Write(ref processing,0); }
    }

    private void ReportReadState(State s,int lines)
    {
        string status;lock(gate){
            var count=s.LastPanel?.Cells.Count(c=>!string.IsNullOrWhiteSpace(c.Text))??(s.LastResult.Length>0?1:0);
            status=s.Busy?$"قُرئت {lines} أسطر · الترجمة قيد المعالجة"
                :s.Candidate.Length==0?"اللوحة موجودة، لكن OCR لم يقرأ نصًا."
                :s.TranslationError.Length>0?"تعذر طلب الترجمة: "+s.TranslationError
                :s.Failures>0?count>0?$"تُعرض {count} أجزاء · بعض الأجزاء لم تُقبل؛ المحاولة التالية تلقائية.":"قُرئ النص، لكن ناتج ترجمته لم يُقبل؛ المحاولة التالية تلقائية."
                :s.Visible&&count>0?$"تُعرض الترجمة · {count} أجزاء"
                :lines>0?$"قُرئت {lines} أسطر · بانتظار ثبات النص":"اللوحة موجودة، لكن OCR لم يقرأ نصًا.";
        }
        Status?.Invoke(s.Area.Id,status);
    }

    private bool SuppressDuplicate(State state,PanelLayout panel,TextBox source)
    {
        State[] matches;State winner;
        static IEnumerable<PanelCell> Body(PanelLayout p)=>p.Cells.Any(c=>!c.Heading)?p.Cells.Where(c=>!c.Heading):p.Cells;
        lock(gate){
            matches=states.Where(other=>other!=state&&other.Area.Kind==state.Area.Kind&&other.PhoneBounds is {} bounds&&other.OwnedMessages is {} existing&&now()-other.PhoneVerifiedAt<=1800&&Intersection(source,bounds)>.65&&Body(panel).Any(c=>Body(existing).Any(n=>PhonePanelTracker.Same(n,c)))).Append(state).ToArray();
            winner=matches.OrderBy(s=>(s.Area.Source.Right-s.Area.Source.Left)*(s.Area.Source.Bottom-s.Area.Source.Top)).ThenBy(s=>states.IndexOf(s)).First();
        }
        foreach(var loser in matches.Where(s=>s!=winner))ClearConflicting(loser);
        if(winner==state)return false;
        Status?.Invoke(state.Area.Id,"المحادثة نفسها تقرؤها بطاقة أدق؛ مُنع عرضها مرتين.");return true;
    }

    private static double Intersection(TextBox a,TextBox b){var overlap=Math.Max(0,Math.Min(a.Right,b.Right)-Math.Max(a.Left,b.Left))*Math.Max(0,Math.Min(a.Bottom,b.Bottom)-Math.Max(a.Top,b.Top));var smaller=Math.Max(.000001,Math.Min((a.Right-a.Left)*(a.Bottom-a.Top),(b.Right-b.Left)*(b.Bottom-b.Top)));return overlap/smaller;}
    public Reading WithoutPhoneText(Reading input,TextBox dialogueArea)=>WithoutMessageText(input,dialogueArea);
    public Reading WithoutMessageText(Reading input,TextBox dialogueArea,bool preserveDialogue=true)
    {
        lock(gate){
            var phones=states.Where(s=>s.Area.Kind is ReadingAreaKind.Phone or ReadingAreaKind.Notification&&s.PhoneBounds is not null&&now()-s.PhoneVerifiedAt<=1800&&s.OwnedMessages is not null).ToArray();
            if(phones.Length==0)return input;
            return new Reading(input.Lines.Where(line=>{
                if(preserveDialogue&&PhoneReadingEvidence.IsDialogue(line.Text))return true;
                if(line.Box is not {} local)return true;var box=local.PlacedIn(dialogueArea);
                foreach(var phone in phones){
                    var bounds=phone.PhoneBounds!.Value;var text=TextMatch.Normalize(line.Text);
                    if(!preserveDialogue&&phone.Area.Kind==ReadingAreaKind.Phone){
                        var occupied=phone.OwnedMessages!.Cells.Select(c=>c.Box).Aggregate((a,b)=>a.Union(b)).PlacedIn(bounds);
                        double overlap=Math.Max(0,Math.Min(occupied.Right,box.Right)-Math.Max(occupied.Left,box.Left))*Math.Max(0,Math.Min(occupied.Bottom,box.Bottom)-Math.Max(occupied.Top,box.Top));
                        if(overlap/Math.Max(.000001,(box.Right-box.Left)*(box.Bottom-box.Top))>=.5)return false;
                    }

                    if(text.Length>=5&&Intersection(box,bounds)>=.8&&phone.OwnedMessages!.Cells.Any(c=>{var key=TextMatch.Normalize(c.Source);return key.Contains(text,StringComparison.Ordinal)||TextMatch.IsSimilar(key,text,.94);}))return false;
                    foreach(var cell in phone.OwnedMessages!.Cells){var b=cell.Box.PlacedIn(phone.PhoneBounds!.Value);
                        double overlap=Math.Max(0,Math.Min(b.Right,box.Right)-Math.Max(b.Left,box.Left))*Math.Max(0,Math.Min(b.Bottom,box.Bottom)-Math.Max(b.Top,box.Top));double area=Math.Max(.000001,(box.Right-box.Left)*(box.Bottom-box.Top));
                        if(overlap/area>=.65)return false;
                    }
                }
                return true;
            }));
        }
    }
    private void ReleaseConflictingOwners()
    {
        State[] pending;lock(gate)pending=states.Where(s=>s.Area.Kind is ReadingAreaKind.Instructions or ReadingAreaKind.MapCard or ReadingAreaKind.Menu&&s.Candidate.Length>0).ToArray();
        foreach(var other in pending){
            Reading input;lock(gate)input=other.SourceReading.Lines.Count>0?other.SourceReading:other.Panel is {} panel?new Reading(panel.Cells.Select(c=>new TextLine(c.Source,c.Box))):new Reading([new TextLine(other.Candidate,new(0,0,1,1))]);
            var source=other.Area.Source;
            if(WithoutMessageText(input,new(source.Left,source.Top,source.Right,source.Bottom),false).Lines.Count<input.Lines.Count)ClearConflicting(other);
        }
    }
    private void ClearConflicting(State s){AreaCaption? clear=null;CancellationTokenSource? cancel;lock(gate){cancel=s.ActiveTranslation;var hadContent=s.Visible||s.Candidate.Length>0;s.Visible=false;s.Candidate=s.Sent=s.LastResult="";s.LastPanel=s.LastSourcePanel=s.Panel=s.OwnedMessages=null;s.PhoneBounds=null;s.Phone.Reset();s.Generation++;if(hadContent){s.Revision++;clear=Make(s,"",true);}}try{cancel?.Cancel();}catch(ObjectDisposedException){}Status?.Invoke(s.Area.Id,"النص تابع لمنطقة أخرى؛ مُنع العرض المكرر.");if(clear is not null)Caption?.Invoke(clear);}

    private void Observe(State s,string text,PanelLayout? panel,Frame frame)
    {
        AreaCaption? clear=null,renewed=null;bool start=false;
        lock(gate)
        {
            if(disposed||!states.Contains(s))return;
            var at=now();
            if(text.Length<2)
            {
                if(s.Misses==0)s.MissingSince=at;
                if((s.Visible||s.Candidate.Length>0)&&++s.Misses>=2&&at-s.MissingSince>=timing.GoneMs){s.Visible=false;s.Sent="";s.Candidate="";s.LastResult="";s.LastPanel=s.LastSourcePanel=null;s.PanelFrame=null;s.OwnedMessages=null;s.PhoneBounds=null;s.Phone.Reset();s.Generation++;s.Revision++;clear=Make(s,"",true);}
            }
            else
            {
                s.Misses=0;s.Seen=at;s.Panel=panel;s.PanelFrame=frame;
                if(!TextMatch.IsSimilar(TextMatch.Normalize(text),TextMatch.Normalize(s.Candidate),.94))
                {s.Candidate=text;s.Since=at;s.RetryAt=0;s.Failures=0;s.TranslationError="";}
                var cellsChanged=panel is not null&&s.LastPanel is not null&&!panel.Cells.Select(c=>c.Source).SequenceEqual(s.LastPanel.Cells.Select(c=>c.Source));
                if((at-s.Since>=timing.SettleMs||(panel is {Cells.Count:>0}?panel.Cells.All(c=>s.Cache.ContainsKey(Key(c.Source))):s.Cache.ContainsKey(Key(text))))&&(s.Candidate!=s.Sent||cellsChanged)&&!s.Busy&&at>=s.RetryAt){s.Busy=true;start=true;}
                if(s.Visible&&s.LastResult.Length>0&&SharesVisibleContent(s.LastPanel,s.Panel,s.Sent==s.Candidate)&&at-s.LastEmit>=s.Area.DurationMs/2){s.LastEmit=at;s.Revision++;renewed=Make(s,s.LastResult) with{Panel=s.LastPanel};}
            }
        }
        if(clear is not null){Trace?.Invoke(new(s.Area.Id,s.Area.Name,"[source gone]","","cleared"));Caption?.Invoke(clear);}if(renewed is not null)Caption?.Invoke(renewed);
        if(start)_=Task.Run(()=>Translate(s));
    }

    private static bool SharesVisibleContent(PanelLayout? previous,PanelLayout? next,bool sameText)
    {
        if(previous is null||next is null)return sameText;
        var body=previous.Cells.Where(c=>!c.Heading).ToArray();if(body.Length==0)body=previous.Cells.ToArray();
        return body.Any(c=>!string.IsNullOrWhiteSpace(c.Text)&&next.Cells.Any(n=>Key(c.Source)==Key(n.Source)));
    }

    private void PublishCached(State s,long generation,bool final,string originalSource)
    {
        AreaCaption? caption=null;AreaTrace? trace=null;string? finalStatus=null;
        lock(gate)
        {
            if(disposed||!states.Contains(s)||s.Generation!=generation||s.Candidate.Length==0)return;
            var panel=s.Panel;var frame=s.PanelFrame;
            var partial=panel is not null&&panel.Cells.Any(c=>!s.Cache.TryGetValue(Key(c.Source),out var value)||string.IsNullOrWhiteSpace(value));
            if(panel is not null){panel=panel with{Cells=panel.Cells.Select(c=>c with{Text=VisibleCellTranslation(c,s.Cache,s.LastSourcePanel,s.LastPanel,panel.Kind=="phone")}).ToArray()};}
            var result=panel is {Cells.Count:>0}?string.Join("\n",panel.Cells.Select(c=>c.Text)):s.Cache.GetValueOrDefault(Key(s.Candidate),"");
            if(final){
                if(s.Candidate!=originalSource){s.Sent="";s.RetryAt=0;}
                else if(partial||string.IsNullOrWhiteSpace(result)){s.Sent="";s.Failures++;s.RetryAt=now()+Math.Min(8000,1000*(1<<Math.Min(3,s.Failures-1)));}
                else {s.Sent=s.Candidate;s.Failures=0;s.TranslationError="";}
                if(partial||string.IsNullOrWhiteSpace(result)){
                    int translated=panel?.Cells.Count(c=>!string.IsNullOrWhiteSpace(c.Text))??0;
                    finalStatus=translated>0?$"تُعرض {translated} أجزاء · بعض الأجزاء لم تُقبل؛ المحاولة التالية تلقائية.":"قُرئ النص، لكن ناتج ترجمته لم يُقبل؛ المحاولة التالية تلقائية.";
                }
            }
            var colorsChanged=panel is not null&&s.LastSourcePanel is not null&&!panel.Cells.Select(c=>(c.Background,c.Foreground)).SequenceEqual(s.LastSourcePanel.Cells.Select(c=>(c.Background,c.Foreground)));
            if(!string.IsNullOrWhiteSpace(result)&&(colorsChanged||result!=s.LastResult||panel is not null&&s.LastPanel is not null&&!panel.Cells.Select(c=>Key(c.Source)).SequenceEqual(s.LastPanel.Cells.Select(c=>Key(c.Source))))){
                if(panel is {Kind:"notification"}){
                    var body=panel.Cells.Where(c=>!c.Heading).ToArray();if(body.Length==0)body=panel.Cells.ToArray();
                    if(!body.Any(c=>!string.IsNullOrWhiteSpace(c.Text)))return;
                    var o=s.Area.PresentationOutput();var scale=Math.Min(1,Math.Min((o.Right-o.Left)*s.FullWidth/panel.Width,(o.Bottom-o.Top)*s.FullHeight/panel.Height));
                    s.Output=new(o.Top,o.Top+panel.Height*scale/s.FullHeight,o.Right-panel.Width*scale/s.FullWidth,o.Right);
                    if(frame is not null)panel=SpatialText.WithImage(frame,panel);
                }
                if(panel is {Kind:"phone"}){
                    var body=panel.Cells.Where(PhonePanelTracker.Body).ToArray();if(!body.Any(c=>!string.IsNullOrWhiteSpace(c.Text)))return;
                    var o=s.Output??s.Area.PresentationOutput();double scale=Math.Max(.01,Math.Min((o.Right-o.Left)*s.FullWidth/panel.Width,(o.Bottom-o.Top)*s.FullHeight/panel.Height));
                    // A pending expansion may carry the old Arabic temporarily. Once the fuller
                    // translation arrives, grow its bubble instead of freezing the old short height.
                    if(s.LastPanel is {Kind:"phone"} previous&&s.LastSourcePanel is {} raw&&panel.Cells.Count==raw.Cells.Count&&panel.Cells.Select((c,i)=>Key(raw.Cells[i].Source)==Key(c.Source)).All(same=>same)&&!panel.Cells.Where((c,i)=>previous.Cells[i].Text.Length>0&&c.Text.Length>previous.Cells[i].Text.Length+2).Any())
                        panel=panel with{Width=previous.Width,Height=previous.Height,Cells=panel.Cells.Select((c,i)=>c with{Box=previous.Cells[i].Box,LineHeight=previous.Cells[i].LineHeight}).ToArray()};
                    else panel=SpatialText.ReflowPhone(panel,s.Area.Appearance?.MatchSize==false?s.Area.FontSize*s.FullWidth/1920d/scale:null);
                }
                s.LastResult=result;s.LastSourcePanel=s.Panel;s.LastPanel=panel;s.LastEmit=now();s.Visible=true;s.Revision++;
                caption=Make(s,result) with{Panel=panel};
                trace=new(s.Area.Id,s.Area.Name,s.Candidate,result,partial?"partial":"shown");
            }
        }
        if(caption is not null){Status?.Invoke(s.Area.Id,$"تُعرض الترجمة · {caption.Panel?.Cells.Count(c=>!string.IsNullOrWhiteSpace(c.Text))??1} أجزاء");Caption?.Invoke(caption);}
        if(trace is not null)Trace?.Invoke(trace);
        if(finalStatus is not null)Status?.Invoke(s.Area.Id,finalStatus);
    }

    private async Task Translate(State s)
    {
        string source;long generation;PanelLayout? panel;long started=now();
        using var cancel=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        lock(gate){if(disposed||!states.Contains(s)||s.Candidate.Length==0){s.Busy=false;return;}source=s.Candidate;panel=s.Panel;generation=s.Generation;s.ActiveTranslation=cancel;}
        Trace?.Invoke(new(s.Area.Id,s.Area.Name,source,"","translating"));
        try
        {
            var pieces=panel is {Cells.Count:>0}?panel.Cells.Select(c=>c.Source).DistinctBy(Key).ToArray():new[]{source};
            foreach(var piece in pieces){if(reviewed?.Invoke(piece) is {Length:>0} ready){lock(gate)s.Cache[Key(piece)]=ready;}}
            var missing=pieces.Where(t=>{lock(gate)return !s.Cache.ContainsKey(Key(t));}).ToArray();
            // First show reusable cells. Each small batch is published immediately; a slow last message
            // or a rejected English cell must not hide the rest of the phone conversation.
            PublishCached(s,generation,false,source);
            int batchSize=s.Area.Kind is ReadingAreaKind.Phone or ReadingAreaKind.Notification?1:Math.Clamp(translator.PreferredBatchSize,1,4);
            foreach(var pendingBatch in missing.Chunk(batchSize))
            {
                lifetime.Token.ThrowIfCancellationRequested();
                string[] batch;
                lock(gate){
                    if(disposed||!states.Contains(s)||s.Generation!=generation)return;
                    // Do not spend the provider on messages which were scrolled out while an earlier item ran.
                    var wanted=s.Panel is {Cells.Count:>0}?s.Panel.Cells.Select(c=>Key(c.Source)).ToHashSet(StringComparer.Ordinal):new HashSet<string>([Key(s.Candidate)],StringComparer.Ordinal);
                    batch=pendingBatch.Where(t=>wanted.Contains(Key(t))).ToArray();
                }
                if(batch.Length==0)continue;
                IReadOnlyList<string> values;
                if(s.Area.Appearance?.ReviewedOnly==true)values=batch.Select(t=>reviewed?.Invoke(t)??"").ToArray();
                else {
                    try{values=await translator.TranslateManyAsync(batch,cancel.Token).ConfigureAwait(false);}
                    catch(TranslatorException error)when(error.Status==TranslatorStatus.OutputRejected){
                        if(batch.Length==1){Trace?.Invoke(new(s.Area.Id,s.Area.Name,batch[0],"","rejected_output",(int)Math.Clamp(now()-started,0,int.MaxValue),error.Status.ToString()));continue;}
                        // Some providers reject the whole batch for one damaged title. Recover the
                        // other cells individually; do not discard an otherwise readable map description.
                        var recovered=new string[batch.Length];
                        for(int i=0;i<batch.Length;i++){
                            try{recovered[i]=await translator.TranslateAsync(batch[i],cancel.Token).ConfigureAwait(false);}
                            catch(TranslatorException invalid)when(invalid.Status==TranslatorStatus.OutputRejected){recovered[i]="";}
                        }
                        values=recovered;
                    }
                }
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
        catch(Exception error){if(!lifetime.IsCancellationRequested){var reason=error is TranslatorException failure?failure.Status.ToString():error.GetType().Name;lock(gate){s.TranslationError=reason;s.RetryAt=now()+1000;}Status?.Invoke(s.Area.Id,"تعذر طلب الترجمة: "+reason);Trace?.Invoke(new(s.Area.Id,s.Area.Name,source,"","failed",(int)Math.Clamp(now()-started,0,int.MaxValue),reason));Failed?.Invoke(s.Area.Id,reason);}}
        finally {lock(gate){s.Busy=false;s.ActiveTranslation=null;}}
    }
    internal static string VisibleCellTranslation(PanelCell cell,IReadOnlyDictionary<string,string> cache,PanelLayout? oldSource,PanelLayout? oldDisplay,bool phone)
    {
        if(cache.TryGetValue(Key(cell.Source),out var value)&&!string.IsNullOrWhiteSpace(value))return value;
        if(!phone||oldSource is null||oldDisplay is null)return "";
        for(int i=0;i<Math.Min(oldSource.Cells.Count,oldDisplay.Cells.Count);i++){
            var prior=oldSource.Cells[i];
            if(Math.Abs(prior.Box.Top-cell.Box.Top)<.06&&PhonePanelTracker.Same(prior,cell)&&!string.IsNullOrWhiteSpace(oldDisplay.Cells[i].Text))return oldDisplay.Cells[i].Text;
        }
        return "";
    }
    private static AreaCaption Make(State s,string text,bool clear=false)=>new(s.Area.Id,s.Revision,text,s.Output??s.Area.PresentationOutput(),
        s.Area.FontSize,s.Area.Background,s.Area.DurationMs,clear,s.Area.Appearance);
    public void Dispose(){lock(gate){if(disposed)return;disposed=true;}lifetime.Cancel();}
}

public sealed record AreaTrace(string AreaId,string AreaName,string Source,string Translation,string Stage,int ElapsedMs=0,string? Error=null);
