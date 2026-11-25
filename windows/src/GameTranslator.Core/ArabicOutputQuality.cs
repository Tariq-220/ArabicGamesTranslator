// SPDX-License-Identifier: AGPL-3.0-only
using System.Text.RegularExpressions;
namespace GameTranslator.Core;

/// <summary>Validate provider output before it can enter display or translation memory.
/// A failed translation is an error, never an English caption or an invented replacement.</summary>
public static partial class ArabicOutputQuality
{
    private static readonly HashSet<string> Acronyms=new(["FIB","IAA","FPS","XP","PSN","DLC","HUD","GTA","TV","USB","HDMI","LB","RB","LT","RT","ESC","ENTER","TAB"],StringComparer.Ordinal);
    public static string DialogueInput(string text)
    {
        var result=new System.Text.StringBuilder();
        foreach(var row in text.Replace("\r\n","\n").Split('\n'))
        {
            var line=Whitespace().Replace(row," ").Trim();if(line.Length==0)continue;
            if(result.Length>0)result.Append(SpeakerLabel().IsMatch(line)?'\n':' ');
            result.Append(line);
        }
        return result.ToString();
    }
    [GeneratedRegex(@"^(?:[-–—]\s*)?[A-Z][A-Za-z'’.-]*(?: [A-Z][A-Za-z'’.-]*){0,2}:\s*")]
    private static partial Regex SpeakerLabel();

    public static bool Accepts(string source, string translated)
    {
        if (string.IsNullOrWhiteSpace(translated)) return false;
        translated=ArabicGameNames.Localize(source,translated);
        var sourceLine=CaptionAppearance.SplitSpeaker(source);var targetLine=CaptionAppearance.SplitSpeaker(translated);
        if(sourceLine.Name.Length>0&&sourceLine.Body.Any(char.IsLetter)&&targetLine.Name.Length>0&&!targetLine.Body.Any(char.IsLetter))return false;
        // A source-grounded speaker label is metadata. An unfamiliar character name must not reject an Arabic body.
        var speakers=source.Split('\n').Select(CaptionAppearance.SplitSpeaker).Where(p=>p.Name.Length>0).Select(p=>p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        translated=string.Join("\n",translated.Split('\n').Select(row=>{var p=CaptionAppearance.SplitSpeaker(row);return p.Name.Length>0&&speakers.Contains(p.Name)?p.Body:row;}));
        var letters = translated.Where(char.IsLetter).ToArray();
        var arabic = letters.Count(c => c is >= '\u0620' and <= '\u064a' or >= '\u066e' and <= '\u06d3' or >= '\u06fa' and <= '\u06ff');
        if (arabic == 0) return false;
        // Preserve source codes/acronyms (FIB, PS5), not untranslated sentences or stray Latin words.
        foreach (Match match in Latin().Matches(translated))
        {
            var word = match.Value;
            var button = word is "A" or "B" or "X" or "Y" && Regex.IsMatch(source,@"\b(?:press|hold|tap|back|select|confirm|cancel|zoom|scroll|rotate|recenter|open|close|next|previous)\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
            var code = button || Acronyms.Contains(word) || Regex.IsMatch(word,@"^[A-Z]{1,5}[0-9]{1,4}$",RegexOptions.CultureInvariant);
            if (!code || !SourceWord(source, word)) return false;
        }
        return arabic >= letters.Length * .55;
    }

    private static bool SourceWord(string source, string word) => Regex.IsMatch(source,
        @"(?<![A-Za-z0-9])" + Regex.Escape(word) + @"(?![A-Za-z0-9])", RegexOptions.CultureInvariant);
    [GeneratedRegex(@"\s+")] private static partial Regex Whitespace();
    [GeneratedRegex(@"[A-Za-z][A-Za-z0-9'-]*")] private static partial Regex Latin();
}
