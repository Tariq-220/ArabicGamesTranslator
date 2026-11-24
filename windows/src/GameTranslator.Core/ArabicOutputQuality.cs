// SPDX-License-Identifier: AGPL-3.0-only
using System.Text.RegularExpressions;
namespace GameTranslator.Core;

/// <summary>Validate provider output before it can enter display or translation memory.
/// A failed translation is an error, never an English caption or an invented replacement.</summary>
public static partial class ArabicOutputQuality
{
    public static string DialogueInput(string text) => Whitespace().Replace(text, " ").Trim();

    public static bool Accepts(string source, string translated)
    {
        if (string.IsNullOrWhiteSpace(translated)) return false;
        var letters = translated.Where(char.IsLetter).ToArray();
        var arabic = letters.Count(c => c is >= '\u0620' and <= '\u064a' or >= '\u066e' and <= '\u06d3' or >= '\u06fa' and <= '\u06ff');
        if (arabic == 0) return false;
        // Preserve source codes/acronyms (FIB, PS5), not untranslated sentences or stray Latin words.
        foreach (Match match in Latin().Matches(translated))
        {
            var word = match.Value;
            var code = word.Any(char.IsDigit) || word.Length is >= 2 and <= 5 && word.All(c => !char.IsLetter(c) || char.IsUpper(c));
            if (!code || !SourceWord(source, word)) return false;
        }
        return arabic >= letters.Length * .55;
    }

    private static bool SourceWord(string source, string word) => Regex.IsMatch(source,
        @"(?<![A-Za-z0-9])" + Regex.Escape(word) + @"(?![A-Za-z0-9])", RegexOptions.CultureInvariant);
    [GeneratedRegex(@"\s+")] private static partial Regex Whitespace();
    [GeneratedRegex(@"[A-Za-z][A-Za-z0-9'-]*")] private static partial Regex Latin();
}
