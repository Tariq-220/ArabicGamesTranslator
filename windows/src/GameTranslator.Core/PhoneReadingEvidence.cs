// SPDX-License-Identifier: AGPL-3.0-only
using System.Text.RegularExpressions;
namespace GameTranslator.Core;

/// <summary>Manual geometry selects where to read, not permission to treat gameplay as a phone.</summary>
public static class PhoneReadingEvidence
{
    public static bool IsDialogue(string text){var(name,_)=CaptionAppearance.SplitSpeaker(text);return name.Length>0&&ArabicGameNames.Speaker(name.TrimEnd(':','：')) is not null;}
    private static bool Distance(string text)=>Regex.IsMatch(text.Trim(),@"^[+\s\d.,]+\s*(?:m|km|mi|ft|me)\s*$",RegexOptions.IgnoreCase);
    public static Reading WithoutGameplay(Reading reading,Frame? verifiedPhone=null)
    {
        var kept=new List<TextLine>();TextBox? previous=null;
        foreach(var line in reading.Lines){
            if(IsDialogue(line.Text)&&!(verifiedPhone is not null&&AuthorOnPhone(verifiedPhone,reading,line))){previous=line.Box;continue;}
            if(Distance(line.Text))continue;
            if(previous is {} p&&line.Box is {} b){var h=Math.Max(p.Bottom-p.Top,b.Bottom-b.Top);if(b.Top>=p.Top&&b.Top-p.Bottom<h*1.4&&Math.Min(p.Right,b.Right)>Math.Max(p.Left,b.Left)){previous=b;continue;}}
            previous=null;kept.Add(line);
        }
        return new Reading(kept);
    }
    private static bool AuthorOnPhone(Frame frame,Reading reading,TextLine author)
    {
        if(author.Box is not {} b||!SpatialText.HasMessageSurface(frame,b))return false;
        return reading.Lines.Any(other=>other!=author&&other.Box is {} n&&n.Top>=.16&&other.Text.Count(char.IsLetter)>=3&&SpatialText.HasMessageSurface(frame,n)
            &&Math.Min(b.Right,n.Right)>Math.Max(b.Left,n.Left)
            &&Math.Max(0,Math.Max(b.Top-n.Bottom,n.Top-b.Bottom))<=Math.Max(.14,Math.Max(b.Bottom-b.Top,n.Bottom-n.Top)*1.8));
    }
    public static bool Incompatible(Reading reading,bool verifiedPhoneAuthors=false)
    {
        var text=reading.Text;
        if(Regex.IsMatch(text,@"\bMAP\b[\s\S]{0,100}\b(?:BRIEF|PROGRESS)\b|\bBRIEF\b[\s\S]{0,80}\bPROGRESS\b|\bPROGRESS\b[\s\S]{0,80}\bBRIEF\b",RegexOptions.IgnoreCase))return true;
        // Store/bank map panels are often inside a phone ROI while their MAP header is outside it.
        // Require two distinct whole UI fields, never prose merely mentioning a shop or destination.
        var fields=reading.Lines.Select(l=>Regex.Match(l.Text.Trim(),@"^(SELL VALUE|REGISTRATION FEE|PARKING SPOTS|TAKE VALUE|CASH DRAWER|VAULT|FEATURES|SECURITY|STYLE)(?:\s*[\d$£€,.()\-–]+)?$",RegexOptions.IgnoreCase)).Where(m=>m.Success).Select(m=>m.Groups[1].Value.ToUpperInvariant()).Distinct().Count();
        if(fields>=2)return true;
        if(!verifiedPhoneAuthors&&reading.Lines.Any(line=>IsDialogue(line.Text)))return true;
        return reading.Lines.Count>0&&reading.Lines.All(l=>Distance(l.Text));
    }
    public static bool Accepts(Frame crop,Reading reading,CropRegion source,bool located,bool knownPage)
    {
        if(VerifiedConversation(crop,reading))return true;
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
    public static bool VerifiedConversation(Frame crop,Reading reading)
    {
        if(Incompatible(reading,true))return false;
        var lines=reading.Lines.Where(l=>l.Box is not null&&l.Text.Count(char.IsLetter)>=2).ToArray();
        return lines.Any(h=>h.Box!.Value.Top<.16&&!h.Text.Contains(':')&&ArabicGameNames.Speaker(h.Text.Trim()) is not null&&SpatialText.HasMessageSurface(crop,h.Box.Value)
            &&lines.Count(l=>l!=h&&l.Box!.Value.Top>h.Box.Value.Bottom+.02&&SpatialText.HasMessageSurface(crop,l.Box.Value))>=2);
    }
}
