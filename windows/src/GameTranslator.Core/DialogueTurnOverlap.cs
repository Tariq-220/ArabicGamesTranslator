// SPDX-License-Identifier: AGPL-3.0-only
namespace GameTranslator.Core;

/// <summary>A fading multi-speaker block can lose its first turn, or gain a new reply.
/// Replace that block instead of stacking the shared turn twice. Distinct speakers/wording remain distinct.</summary>
internal static class DialogueTurnOverlap
{
    private static List<(string Name,string Body,bool Question)> Turns(string text)
    {
        var result=new List<(string,string,bool)>();string name="";var body=new List<string>();
        void Flush(){if(body.Count>0){var value=string.Join(' ',body);result.Add((name,TextMatch.Normalize(value),value.Contains('?')||value.Contains('؟')));}body.Clear();}
        foreach(var row in text.Replace("\r\n","\n").Split('\n')){
            var(label,value)=CaptionAppearance.SplitSpeaker(row);
            if(label.Length>0){Flush();name=ArabicGameNames.CanonicalSpeaker(label.TrimEnd(':','：')).ToLowerInvariant();}
            if(!string.IsNullOrWhiteSpace(value))body.Add(value);
        }
        Flush();return result;
    }
    public static bool SharesTurn(string newer,string previous)
    {
        var next=Turns(newer);var old=Turns(previous);
        return (next.Count>1||old.Count>1)&&next.Any(n=>n.Name.Length>0&&n.Body.Length>=12&&old.Any(p=>n.Name==p.Name&&n.Body==p.Body&&n.Question==p.Question));
    }
}
