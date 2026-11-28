// SPDX-License-Identifier: AGPL-3.0-only
namespace GameTranslator.Core;

/// <summary>Repair a noisy/missing label only when a recently confirmed speaker said the identical body.
/// Never infer a speaker for a new sentence or replace a different known speaker.</summary>
public sealed class SpeakerReadingContinuity
{
    private string speaker="",body="";private long confirmedAt=long.MinValue/2;
    public string Filter(string source,long now)
    {
        var (label,text)=CaptionAppearance.SplitSpeaker(source);
        var name=label.Trim().TrimEnd(':','：').Trim();
        var normalized=TextMatch.Normalize(text)+(text.Contains('?')?"?":"");
        if(name.Length>0&&ArabicGameNames.Speaker(name) is not null){
            speaker=name;body=normalized;confirmedAt=now;return source;
        }
        if(speaker.Length==0||now-confirmedAt>1500||normalized.Length<8||normalized!=body)return source;
        if(name.Length==0||OneEdit(name,speaker))return speaker+": "+text;
        return source;
    }
    public void Reset(){speaker="";body="";confirmedAt=long.MinValue/2;}
    private static bool OneEdit(string first,string second)
    {
        var a=first.ToLowerInvariant();var b=second.ToLowerInvariant();
        if(Math.Abs(a.Length-b.Length)>1)return false;
        int i=0,j=0,changes=0;
        while(i<a.Length&&j<b.Length){if(a[i]==b[j]){i++;j++;continue;}if(++changes>1)return false;if(a.Length>=b.Length)i++;if(b.Length>=a.Length)j++;}
        return changes+(a.Length-i)+(b.Length-j)<=1;
    }
}
