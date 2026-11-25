// SPDX-License-Identifier: AGPL-3.0-only
namespace GameTranslator.Core;

/// <summary>Retain a coherent phone page across partial OCR frames. New pages are confirmed;
/// unchanged messages keep their source key, geometry and measured colours.</summary>
public sealed class PhonePanelTracker
{
    private PanelLayout? current,pending;
    private long pendingAt,newPageAt;
    private int confirmations;
    private readonly Dictionary<string,long> seen=new(StringComparer.Ordinal);
    private static string Key(PanelCell c)=>TextMatch.Normalize(c.Source)+(c.Source.Contains('?')?"?":"");
    public static bool Body(PanelCell c){if(c.Heading)return false;var text=c.Source.Trim();if(System.Text.RegularExpressions.Regex.IsMatch(text,@"^(?:New messages?|Locating\.*)$",System.Text.RegularExpressions.RegexOptions.IgnoreCase))return false;var words=text.Split(' ',StringSplitOptions.RemoveEmptyEntries);return text.StartsWith("Map Pin ",StringComparison.OrdinalIgnoreCase)||text.IndexOfAny(['!','?','…'])>=0||words.Skip(1).Any(w=>w.Length>0&&char.IsLower(w[0]))||new[]{"Hey","Hi","Hello","Okay","OK","Thanks","Yes","No"}.Contains(text,StringComparer.OrdinalIgnoreCase);}
    public void Reset(){current=pending=null;seen.Clear();confirmations=0;newPageAt=0;}
    private static bool Negation(string s)=>System.Text.RegularExpressions.Regex.IsMatch(s,@"\b(?:not|no|never|cannot)\b|n['’]t\b",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    private static bool Same(PanelCell a,PanelCell b)
    {
        if(Key(a)==Key(b))return true;
        var x=TextMatch.Normalize(a.Source);var y=TextMatch.Normalize(b.Source);
        return x.Split(' ').Length>=3&&x.Split(' ').Length==y.Split(' ').Length&&Negation(a.Source)==Negation(b.Source)
            &&string.Concat(a.Source.Where(char.IsDigit))==string.Concat(b.Source.Where(char.IsDigit))&&TextMatch.IsSimilar(x,y,.96);
    }
    private static double Shift(TextBox a,TextBox b)=>Math.Max(Math.Max(Math.Abs(a.Left-b.Left),Math.Abs(a.Top-b.Top)),Math.Max(Math.Abs(a.Right-b.Right),Math.Abs(a.Bottom-b.Bottom)));
    private static bool Overlap(TextBox a,TextBox b)=>Math.Min(a.Right,b.Right)-Math.Max(a.Left,b.Left)>.05&&Math.Min(a.Bottom,b.Bottom)-Math.Max(a.Top,b.Top)>.008;
    public PanelLayout? Observe(PanelLayout next,long at)
    {
        if(!next.Cells.Any(Body))return null;
        if(current is null){current=next;foreach(var c in next.Cells)seen[Key(c)]=at;return next;}
        var shared=next.Cells.Count(n=>Body(n)&&current.Cells.Any(p=>Same(p,n)&&Shift(p.Box,n.Box)<.06));
        bool headerChanged=current.Cells.FirstOrDefault(c=>c.Heading) is {} oldHeader&&next.Cells.FirstOrDefault(c=>c.Heading) is {} newHeader&&!Same(oldHeader,newHeader);
        bool newPage=headerChanged||shared==0;
        if(newPage){
            if(pending is null)newPageAt=at;
            var samePending=pending is not null&&pending.Cells.Select(Key).SequenceEqual(next.Cells.Select(Key));
            if(!samePending){pending=next;pendingAt=at;confirmations=1;return at-newPageAt>600?null:current;}
            if(++confirmations<2||at-pendingAt<60)return at-newPageAt>600?null:current;
            current=next;pending=null;seen.Clear();foreach(var c in next.Cells)seen[Key(c)]=at;return next;
        }
        pending=null;confirmations=0;
        var cells=new List<PanelCell>();
        foreach(var n in next.Cells){
            var prior=current.Cells.FirstOrDefault(p=>Same(p,n)&&Shift(p.Box,n.Box)<.06);
            var cell=prior is null?n:n with{Source=prior.Source,Background=prior.Background,Foreground=prior.Foreground,LineHeight=prior.LineHeight,Box=Shift(prior.Box,n.Box)<.018?prior.Box:n.Box};
            cells.Add(cell);seen[Key(cell)]=at;
        }
        foreach(var p in current.Cells.Where(p=>!cells.Any(n=>Same(p,n))))if(at-seen.GetValueOrDefault(Key(p),at)<=1400&&!cells.Any(n=>Overlap(p.Box,n.Box)))cells.Add(p);
        current=next with{Cells=cells.OrderBy(c=>c.Box.Top).ToArray()};return current;
    }
}
