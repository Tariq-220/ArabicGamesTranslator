// SPDX-License-Identifier: AGPL-3.0-only
namespace GameTranslator.Core;

/// <summary>Small reviewed expressions from the reported examples; never rewrite fragments of a longer sentence.</summary>
public static class ReviewedArabicExpressions
{
    public static string? Find(string source)
    {
        var (label,body)=CaptionAppearance.SplitSpeaker(source);
        var key=body.Trim().ToLowerInvariant().Replace('’','\'');
        var translated=key switch {
            "suck my fuckin' nuts!" or "suck my fucking nuts!" or "suck my balls!"=>"تبًّا لك، أيها الوغد!",
            _=>null
        };
        if(translated is null)return null;
        if(label.Length==0)return translated;
        var speaker=ArabicGameNames.Speaker(label.TrimEnd(':','：').Trim());
        return speaker is null?null:speaker+": "+translated;
    }
}
