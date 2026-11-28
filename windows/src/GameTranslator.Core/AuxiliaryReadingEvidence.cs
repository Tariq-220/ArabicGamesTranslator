// SPDX-License-Identifier: AGPL-3.0-only
using System.Text.RegularExpressions;
using System.Globalization;
using System.Text;
namespace GameTranslator.Core;

/// <summary>Area geometry alone is not evidence of a mission or map description.</summary>
public static class AuxiliaryReadingEvidence
{
    public static bool IsControl(string text)
    {
        if(ButtonPrompts.IsPrompt(text))return true;
        var plain=string.Concat(text.Normalize(NormalizationForm.FormD).Where(c=>CharUnicodeInfo.GetUnicodeCategory(c)!=UnicodeCategory.NonSpacingMark));
        plain=Regex.Replace(plain,@"[^A-Za-z0-9()]+"," ").Trim();
        return Regex.IsMatch(plain,
            @"^(?:(?:EXIT|BACK|BICK|SELECT|SELEC|SELEI|PHOTO STUDIO|PHOTO STIJDIO|PHOTO MODE|SURFACE|RESTORE|ALL[O0]?|DEFAUL[T]?[O0]?|PAUSE PREVIEW|O|X|A|B|Y|LB|RB|LT|RT|L[123]|R[123])(?:\s+|$))+$|^(?:TO TRUNK|TAKE (?:AMMO )?PACK)\s*\(\d+\)\s*[OX]?$",
            RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
    }
    public static Reading Select(Reading reading,ReadingAreaKind kind)
    {
        if(kind is not (ReadingAreaKind.Instructions or ReadingAreaKind.MapCard))return reading;
        var result=new Reading(reading.Lines.Where(l=>!IsControl(l.Text)&&!PhoneReadingEvidence.IsDialogue(l.Text)&&l.Text.Count(char.IsLetter)>=2&&
            (kind!=ReadingAreaKind.Instructions||!Regex.IsMatch(l.Text.Trim(),@"^[•·\s]*(?:a\s*)?ct$|^NIN$",RegexOptions.IgnoreCase))));
        if(kind!=ReadingAreaKind.MapCard)return result;
        // A dark patch behind a vehicle/name/HUD is insufficient. A map description has prose;
        // its header may be outside the user's crop. Preserve all rows when prose is present.
        var prose=result.Lines.Any(l=>Regex.Matches(l.Text,@"[A-Za-z]{2,}") is {Count:>=3} words&&words.Any(w=>w.Value.Any(char.IsLower)));
        return prose?result:Reading.Empty;
    }
}
