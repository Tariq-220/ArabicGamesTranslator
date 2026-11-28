// SPDX-License-Identifier: AGPL-3.0-only
using System.Text.RegularExpressions;
namespace GameTranslator.Core;

/// <summary>Remove only an entire long Arabic answer repeated verbatim by the output pipeline.
/// Never shorten source repetition, separate speakers, partial echoes or a changed number/negation.</summary>
public static class ArabicRepetition
{
    public static string Normalize(string source,string output)
    {
        var(label,body)=CaptionAppearance.SplitSpeaker(output);
        if(body.Contains('\n')||source.Contains('\n')||!body.Any(c=>c is >= '\u0620' and <= '\u064a'))return output;
        var words=Regex.Matches(body,@"\S+").ToArray();
        if(words.Length is <10 or >320||SourceRepeats(source))return output;
        var normalized=words.Select(m=>TextMatch.Normalize(m.Value)).ToArray();
        for(int copies=4;copies>=2;copies--){
            if(words.Length%copies!=0)continue;
            int count=words.Length/copies;if(count<5)continue;
            if(Enumerable.Range(count,words.Length-count).All(i=>normalized[i].Length>0&&normalized[i]==normalized[i%count]))
                return (label.Length>0?label+" ":"")+body[..(words[count-1].Index+words[count-1].Length)].TrimEnd();
        }
        return output;
    }
    private static bool SourceRepeats(string source)
    {
        var words=TextMatch.Normalize(CaptionAppearance.SplitSpeaker(source).Body).Split(' ',StringSplitOptions.RemoveEmptyEntries);
        if(words.Length>320)return true;
        // Any repeated multiword phrase could have been intentional emphasis. Prefer keeping it.
        for(int i=0;i+1<words.Length;i++)for(int j=i+2;j+1<words.Length;j++)
            if(words[i]==words[j]&&words[i+1]==words[j+1])return true;
        return false;
    }
}
