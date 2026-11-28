// SPDX-License-Identifier: AGPL-3.0-only
using System.Text.RegularExpressions;
namespace GameTranslator.Core;

/// <summary>At a sentence start, OCR can read a lowercase l as uppercase I (COOI dog).
/// Repair only a unique dictionary-confirmed spelling, preserving labels, names, acronyms and other wording.</summary>
public static class OcrGlyphRepair
{
    public static string Repair(string source,ISpellDictionary dictionary)
    {
        var(label,body)=CaptionAppearance.SplitSpeaker(source);
        var match=Regex.Match(body,@"^\s*[“\""'«(]*(?<word>[A-Za-z]{4,20})(?=\s+[a-z])",RegexOptions.CultureInvariant);
        if(!match.Success)return source;var word=match.Groups["word"].Value;
        var positions=Enumerable.Range(1,word.Length-1).Where(i=>word[i]=='I').ToArray();
        if(positions.Length is <1 or >2||ArabicGameNames.Speaker(word) is not null||dictionary.Contains(word.ToLowerInvariant()))return source;
        var options=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for(int mask=1;mask<(1<<positions.Length);mask++){
            var candidate=word.ToCharArray();for(int i=0;i<positions.Length;i++)if((mask&(1<<i))!=0)candidate[positions[i]]=word.All(char.IsUpper)?'L':'l';
            var value=new string(candidate);if(dictionary.Contains(value.ToLowerInvariant()))options.Add(value);
        }
        if(options.Count!=1)return source;
        var token=match.Groups["word"];var repaired=body[..token.Index]+options.Single()+body[(token.Index+token.Length)..];
        return label.Length>0?label+" "+repaired:repaired;
    }
}
