// SPDX-License-Identifier: AGPL-3.0-only
using System.Text.RegularExpressions;
namespace GameTranslator.Core;

/// <summary>Shared subtitle register, not per-sentence corrections. Keep meaning and explicit language;
/// only unambiguous lexical forms are changed. Ambiguous dialect must be translated from the English again.</summary>
public static partial class ArabicRegister
{
    public static string Normalize(string text)
    {
        text=Profanity().Replace(text,m=>(m.Groups[1].Success?"ال":"")+"عاهرة");
        text=PluralProfanity().Replace(text,m=>(m.Groups[1].Success?"ال":"")+"عاهرات");
        text=Variants().Replace(text,m=>m.Value switch{"هذي" or "هاذي"=>"هذه","هاذا"=>"هذا","يلا" or "يلّا"=>"هيا",_=>m.Value});
        return text;
    }
    public static bool NeedsRephrase(string source,string translated)
    {
        var body=CaptionAppearance.SplitSpeaker(Normalize(translated)).Body;
        // Do not mistake a quoted character name for a regional negation.
        if(Regex.IsMatch(source,@"\bMo\b",RegexOptions.CultureInvariant))body=Regex.Replace(body,@"(?<![\p{L}])مو(?![\p{L}])","");
        return Dialect().IsMatch(body);
    }
    public const string Instruction="Use concise, natural Modern Standard Arabic, not regional dialect. Preserve the scene meaning, negation and explicit language; do not soften insults or translate idioms word for word.";
    [GeneratedRegex(@"(?<![\p{L}])(ال)?(?:قحب[ةه]|شرموط[ةه])(?![\p{L}])")]
    private static partial Regex Profanity();
    [GeneratedRegex(@"(?<![\p{L}])(ال)?(?:قحاب|شراميط)(?![\p{L}])")]
    private static partial Regex PluralProfanity();
    [GeneratedRegex(@"(?<![\p{L}])(?:هذي|هاذي|هاذا|يلا|يلّا)(?![\p{L}])")]
    private static partial Regex Variants();
    [GeneratedRegex(@"(?<![\p{L}])(?:إيش|ايش|وش|شلون|شحال|تبغى|يبغى|أبغى|ابغى|عايز|عاوز|عايزة|عاوزة|عشان|علشان|اللي|مو|مش|لسه|لسّا|هالشي|هالشيء|هالأمر|هاذول|هذول|هذولا|دحين|دلوقتي|حأعمل|تبي)(?![\p{L}])")]
    private static partial Regex Dialect();
}
