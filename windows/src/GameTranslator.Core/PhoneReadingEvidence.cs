// SPDX-License-Identifier: AGPL-3.0-only
using System.Text.RegularExpressions;
namespace GameTranslator.Core;

/// <summary>Manual geometry selects where to read, not permission to treat gameplay as a phone.</summary>
public static class PhoneReadingEvidence
{
    public static bool IsDialogue(string text){var(name,_)=CaptionAppearance.SplitSpeaker(text);return name.Length>0&&ArabicGameNames.Speaker(name.TrimEnd(':','：')) is not null;}
    private static bool Distance(string text)=>Regex.IsMatch(text.Trim(),@"^[+\s\d.,]+\s*(?:m|km|mi|ft|me)\s*$",RegexOptions.IgnoreCase);
    public static Reading WithoutGameplay(Reading reading)
    {
        var kept=new List<TextLine>();TextBox? previous=null;
        foreach(var line in reading.Lines){
            if(IsDialogue(line.Text)){previous=line.Box;continue;}
            if(Distance(line.Text))continue;
            if(previous is {} p&&line.Box is {} b){var h=Math.Max(p.Bottom-p.Top,b.Bottom-b.Top);if(b.Top>=p.Top&&b.Top-p.Bottom<h*1.4&&Math.Min(p.Right,b.Right)>Math.Max(p.Left,b.Left)){previous=b;continue;}}
            previous=null;kept.Add(line);
        }
        return new Reading(kept);
    }
    public static bool Incompatible(Reading reading)
    {
        var text=reading.Text;
        if(Regex.IsMatch(text,@"\bMAP\b[\s\S]{0,100}\b(?:BRIEF|PROGRESS)\b|\bBRIEF\b[\s\S]{0,80}\bPROGRESS\b",RegexOptions.IgnoreCase))return true;
        if(reading.Lines.Any(line=>IsDialogue(line.Text)))return true;
        return reading.Lines.Count>0&&reading.Lines.All(l=>Distance(l.Text));
    }
    public static bool Accepts(Frame crop,Reading reading,CropRegion source,bool located,bool knownPage)
    {
        if(Incompatible(reading))return false;
        var surface=SpatialText.PhoneSurfacePresent(crop,new(0,1,0,1))||reading.Lines.Any(l=>l.Box is {} b&&SpatialText.HasMessageSurface(crop,b));
        if(!surface)return false;
        if(SpatialText.IsConversation(reading))return true;
        if(knownPage&&SpatialText.PhoneSurfacePresent(crop,new(0,1,0,1)))return true;
        var cells=reading.Lines.Where(l=>l.Box is not null).ToArray();
        var bodies=cells.Where(c=>PhonePanelTracker.Body(new(c.Text,"",c.Box!.Value,"#FFFFFF","#20232A",.03))).ToArray();
        if(bodies.Length==0)return false;
        var precise=(source.Right-source.Left)*(source.Bottom-source.Top)<=.45||crop.Height>=crop.Width*1.2;
        if(cells.Length==1)return (located||precise)&&SpatialText.HasMessageSurface(crop,cells[0].Box!.Value);
        return (located||precise&&crop.Height>=crop.Width*.9)&&bodies.All(c=>SpatialText.HasMessageSurface(crop,c.Box!.Value));
    }
}
