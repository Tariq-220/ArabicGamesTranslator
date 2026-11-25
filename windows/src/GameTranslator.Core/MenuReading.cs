// SPDX-License-Identifier: AGPL-3.0-only
namespace GameTranslator.Core;

/// <summary>Pause UI and map cards are not street labels or dialogue. Geometry stays in source coordinates.</summary>
public static class MenuReading
{
    private static string Label(string text)=>string.Join(' ',text.ToUpperInvariant().Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries)).Trim(' ',':');
    private static readonly HashSet<string> Headers=new(["HELP","SETTINGS","OPTIONS","MAP","AUDIO","DISPLAY","GRAPHICS","CONTROLS","ACCESSIBILITY","BRIEF","PROGRESS","GAME","ONLINE","PAUSE"]);
    public static bool SettingsPage(Reading reading)=>reading.Lines.Any(l=>l.Box is {} b&&b.Top<.16&&System.Text.RegularExpressions.Regex.IsMatch(Label(l.Text),@"\b(?:SETTINGS|HELP|AUDIO|DISPLAY|GRAPHICS|CONTROLS|ACCESSIBILITY)\b"));
    public static Reading Select(Frame frame,Reading reading,bool includeDescription=true)
    {
        var labels=reading.Lines.Select(l=>Label(l.Text)).ToArray();
        bool Term(string text,string term)=>System.Text.RegularExpressions.Regex.IsMatch(text,@"(?<![A-Z])"+term+@"(?![A-Z])");
        bool topHeader=reading.Lines.Any(l=>l.Box is {} b&&b.Top<.15&&Headers.Any(h=>Term(Label(l.Text),h)));
        if(!topHeader&&!labels.Any(Headers.Contains))return Reading.Empty;
        bool map=labels.Any(x=>Term(x,"MAP"))&&(labels.Any(x=>x.Contains("RECENTER")||x.Contains("ADD MARKER")||x.Contains("WAYPOINT"))||labels.Any(x=>Term(x,"BRIEF")||Term(x,"PROGRESS")));
        if(!map)return reading;
        return new(reading.Lines.Where(l=>l.Box is {} b&&(b.Bottom<.13 || b.Top>.88 || includeDescription&&b.Left<.5&&b.Bottom<.57&&SpatialText.HasDarkBackground(frame,b))));
    }
}
