// SPDX-License-Identifier: AGPL-3.0-only
namespace GameTranslator.Core;

/// <summary>Retain a coherent phone page across partial OCR frames. New pages are confirmed;
/// unchanged messages keep their source key, geometry and measured colours.</summary>
public sealed class PhonePanelTracker
{
    private PanelLayout? current,pending;
    private long pendingAt,newPageAt,lastContentAt;
    private int confirmations;
    private CropRegion? capture;private long captureSeen;
    public CropRegion? Locate(CropRegion? detected,long at){
        if(detected is null)return capture is not null&&at-captureSeen<=1500?capture:null;
        captureSeen=at;
        if(capture is {} p){double common=Math.Max(0,Math.Min(p.Right,detected.Right)-Math.Max(p.Left,detected.Left))*Math.Max(0,Math.Min(p.Bottom,detected.Bottom)-Math.Max(p.Top,detected.Top));double union=(p.Right-p.Left)*(p.Bottom-p.Top)+(detected.Right-detected.Left)*(detected.Bottom-detected.Top)-common;
            if(union>0&&common/union>.65)detected=new(p.Top*.75+detected.Top*.25,p.Bottom*.75+detected.Bottom*.25,p.Left*.75+detected.Left*.25,p.Right*.75+detected.Right*.25);
        }
        return capture=detected;
    }
    private readonly Dictionary<string,long> seen=new(StringComparer.Ordinal);
    private readonly Dictionary<string,(int Color,int Count)> colorVotes=new(StringComparer.Ordinal);
    private static string Key(PanelCell c)=>TextMatch.Normalize(c.Source)+(c.Source.Contains('?')?"?":"");
    public static bool Body(PanelCell c){if(c.Heading)return false;var text=c.Source.Trim();if(System.Text.RegularExpressions.Regex.IsMatch(text,@"^(?:New messages?|Locating\.*)$",System.Text.RegularExpressions.RegexOptions.IgnoreCase))return false;var words=text.Split(' ',StringSplitOptions.RemoveEmptyEntries);return text.StartsWith("Map Pin ",StringComparison.OrdinalIgnoreCase)||text.IndexOfAny(['!','?','…'])>=0||words.Skip(1).Any(w=>w.Length>0&&char.IsLower(w[0]))||new[]{"Hey","Hi","Hello","Okay","OK","Thanks","Yes","No"}.Contains(text,StringComparer.OrdinalIgnoreCase);}
    public void Reset(){current=pending=null;capture=null;seen.Clear();colorVotes.Clear();confirmations=0;newPageAt=lastContentAt=0;}
    private static int BubbleColor(string value){if(value.Length!=7||value[0]!='#'||!int.TryParse(value.AsSpan(1),System.Globalization.NumberStyles.HexNumber,System.Globalization.CultureInfo.InvariantCulture,out var rgb))return 0;var r=(rgb>>16)&255;var g=(rgb>>8)&255;var b=rgb&255;return b>150&&g>75&&b-r>45?1:Math.Min(r,Math.Min(g,b))>125?2:0;}
    private static bool Negation(string s)=>System.Text.RegularExpressions.Regex.IsMatch(s,@"\b(?:not|no|never|cannot)\b|n['’]t\b",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    internal static bool Same(PanelCell a,PanelCell b)
    {
        if(Key(a)==Key(b))return true;
        if(a.Heading&&b.Heading&&TextMatch.IsSimilar(TextMatch.Normalize(a.Source),TextMatch.Normalize(b.Source),.72))return true;
        var oldText=TextMatch.Normalize(a.Source);var newText=TextMatch.Normalize(b.Source);
        if(Negation(a.Source)!=Negation(b.Source)||string.Concat(a.Source.Where(char.IsDigit))!=string.Concat(b.Source.Where(char.IsDigit)))return false;
        bool Extends(string longer,string shorter)=>shorter.Split(' ').Length>=3&&shorter.Length>=longer.Length*.45&&(longer.StartsWith(shorter+" ",StringComparison.Ordinal)||longer.EndsWith(" "+shorter,StringComparison.Ordinal));
        if(Extends(oldText,newText)||Extends(newText,oldText))return true;
        return oldText.Split(' ').Length>=4&&newText.Split(' ').Length>=4&&Math.Min(oldText.Length,newText.Length)>=Math.Max(oldText.Length,newText.Length)*.78&&TextMatch.IsSimilar(oldText,newText,.92);
    }
    private static double Shift(TextBox a,TextBox b)=>Math.Max(Math.Max(Math.Abs(a.Left-b.Left),Math.Abs(a.Top-b.Top)),Math.Max(Math.Abs(a.Right-b.Right),Math.Abs(a.Bottom-b.Bottom)));
    private static bool Overlap(TextBox a,TextBox b)=>Math.Min(a.Right,b.Right)-Math.Max(a.Left,b.Left)>.05&&Math.Min(a.Bottom,b.Bottom)-Math.Max(a.Top,b.Top)>.008;
    public bool MatchesCurrentPage(PanelLayout next)=>current is not null&&next.Cells.Any(n=>Body(n)&&current.Cells.Any(p=>Body(p)&&Same(p,n)&&Shift(p.Box,n.Box)<.06));
    public PanelLayout? Observe(PanelLayout next,long at)
    {
        bool Content(PanelCell c)=>next.Kind=="notification"?(!c.Heading||!next.Cells.Any(x=>!x.Heading)&&System.Text.RegularExpressions.Regex.IsMatch(c.Source,@"^(?:Saved|Saving|Paused)$",System.Text.RegularExpressions.RegexOptions.IgnoreCase)):Body(c);
        bool headerChanged=current?.Cells.FirstOrDefault(c=>c.Heading) is {} oldHeader&&next.Cells.FirstOrDefault(c=>c.Heading) is {} newHeader&&!Same(oldHeader,newHeader)&&!TextMatch.IsSimilar(TextMatch.Normalize(oldHeader.Source),TextMatch.Normalize(newHeader.Source),.72);
        if(!next.Cells.Any(Content))return !headerChanged&&next.Kind=="phone"&&current is not null&&at-lastContentAt<=900?current:null;
        lastContentAt=at;
        if(current is null){current=next;foreach(var c in next.Cells)seen[Key(c)]=at;return next;}
        if(!headerChanged)next=next with{Cells=next.Cells.Select(n=>{
            var old=current.Cells.FirstOrDefault(p=>!p.Heading&&!n.Heading&&Shift(p.Box,n.Box)<.04&&PartialReread(n.Source,p.Source));
            return old is null?n:n with{Source=old.Source};
        }).ToArray()};
        var shared=next.Cells.Count(n=>Content(n)&&current.Cells.Any(p=>Same(p,n)&&Shift(p.Box,n.Box)<.06));
        bool newPage=headerChanged||shared==0;
        if(newPage){
            if(pending is null)newPageAt=at;
            var samePending=pending is not null&&pending.Cells.Where(Content).Any(p=>next.Cells.Any(n=>Content(n)&&Same(p,n)&&Shift(p.Box,n.Box)<.06));
            if(!samePending){pending=next;pendingAt=at;confirmations=1;return at-newPageAt>600?null:current;}
            if(++confirmations<2||at-pendingAt<60)return at-newPageAt>600?null:current;
            current=next;pending=null;seen.Clear();colorVotes.Clear();foreach(var c in next.Cells)seen[Key(c)]=at;return next;
        }
        pending=null;confirmations=0;
        var cells=new List<PanelCell>();
        foreach(var n in next.Cells){
            var prior=current.Cells.FirstOrDefault(p=>Same(p,n)&&Shift(p.Box,n.Box)<.06);
            var grows=prior is not null&&(n.Heading&&ArabicGameNames.Speaker(n.Source) is not null&&ArabicGameNames.Speaker(prior.Source) is null||Key(n).Length>Key(prior).Length&&(Key(n).StartsWith(Key(prior)+" ",StringComparison.Ordinal)||Key(n).EndsWith(" "+Key(prior),StringComparison.Ordinal)));
            var acceptColor=false;
            if(prior is not null){var key=Key(prior)+"@"+prior.Box.Top.ToString("F3",System.Globalization.CultureInfo.InvariantCulture);var oldColor=BubbleColor(prior.Background);var newColor=BubbleColor(n.Background);
                if(oldColor!=0&&newColor!=0&&oldColor!=newColor){if(colorVotes.Count>128)colorVotes.Clear();var vote=colorVotes.GetValueOrDefault(key);vote=(newColor,vote.Color==newColor?vote.Count+1:1);colorVotes[key]=vote;acceptColor=vote.Count>=2;if(acceptColor)colorVotes.Remove(key);}else colorVotes.Remove(key);
            }
            var cell=prior is null?n:n with{Source=grows?n.Source:prior.Source,Background=acceptColor?n.Background:prior.Background,Foreground=acceptColor?n.Foreground:prior.Foreground,LineHeight=prior.LineHeight,Box=prior.Box};
            cells.Add(cell);seen[Key(cell)]=at;
        }
        foreach(var p in current.Cells.Where(p=>!cells.Any(n=>Same(p,n))))if(at-seen.GetValueOrDefault(Key(p),at)<=4000&&!cells.Any(n=>Overlap(p.Box,n.Box)))cells.Add(p);
        current=current with{Cells=cells.OrderBy(c=>c.Box.Top).ToArray()};return current;
    }
    private static bool PartialReread(string next,string previous)
    {
        if(Negation(next)!=Negation(previous)||string.Concat(next.Where(char.IsDigit))!=string.Concat(previous.Where(char.IsDigit)))return false;
        static string[] Words(string s)=>TextMatch.Normalize(s).Split(' ',StringSplitOptions.RemoveEmptyEntries).Where(w=>w.Any(char.IsLetter)&&(w.Length>2||SubtitleStabilizer.LooksClean(w))).ToArray();
        var newer=Words(next);var older=Words(previous);
        if(newer.Length<5||newer.Length>=older.Length||TextMatch.Normalize(next).Length<TextMatch.Normalize(previous).Length*.55)return false;
        int cursor=0;
        foreach(var word in newer){
            bool Match(string old)=>word==old||word.Length>=5&&old.Length>=5&&SpeakerReadingContinuity.OneEdit(word,old)||word.Length>=4&&old.Length>=6&&old.StartsWith(word,StringComparison.Ordinal);
            while(cursor<older.Length&&!Match(older[cursor]))cursor++;
            if(cursor++>=older.Length)return false;
        }
        return true;
    }
}
