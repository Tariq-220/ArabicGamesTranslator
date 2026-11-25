// SPDX-License-Identifier: AGPL-3.0-only

using System.Text.RegularExpressions;

namespace GameTranslator.Core;

/// <summary>
/// Kare iki kez okunur: olduğu gibi ve yalnız beyaz yazısı bırakılarak. Birçok oyunda konuşmacı adı renklidir; beyaz
/// okuma sözü temiz verir ama adı düşürür, ham okuma adı verir ama sözü gürültülü okuyabilir. Hangisi seçilirse
/// seçilsin öteki bir şey kaybettirir: ad bir karede görünüp ötekinde kayboluyor, aynı altyazı bir adlı bir adsız iki
/// kez çevriliyordu. Burada beyaz okumanın sözü ham okumanın adıyla birleştirilir.
/// </summary>
public static partial class SpeakerMerge
{
    /// <summary>
    /// <paramref name="chosen"/> (seçilen okuma) adsızsa ve <paramref name="other"/> aynı sözü konuşmacı adıyla
    /// okuduysa, ad seçilen okumanın ilk satırına eklenir; değilse seçilen okuma olduğu gibi döner.
    /// </summary>
    public static Reading WithSpeakerFrom(Reading chosen, Reading other)
    {
        if (chosen.Lines.Count == 0 || other.Lines.Count == 0) return chosen;
        var first = chosen.Lines[0];
        if (Label().IsMatch(first.Text)) return chosen;
        // Ad, öteki okumada aynı sözün başındaki satırdadır (çoğunlukla ilk satır).
        foreach (var line in other.Lines)
        {
            var match = Label().Match(line.Text);
            if (!match.Success) continue;
            var said = line.Text[match.Length..].Trim();
            if (said.Length == 0 || !SameWords(said, first.Text)) continue;
            var box = first.Box is { } a && line.Box is { } b ? a.Union(b) : first.Box ?? line.Box;
            var merged = first with { Text = $"{match.Groups["name"].Value}: {first.Text.Trim()}", Box = box };
            return new Reading([merged, .. chosen.Lines.Skip(1)]);
        }
        return chosen;
    }

    /// <summary>A coloured name alone is not a complete utterance. Prefer the other OCR pass if it has the body.</summary>
    public static bool HasBody(Reading reading)
    {
        var text=reading.Text.Trim();var match=Label().Match(text);
        if(match.Success)return text[match.Length..].Any(char.IsLetter);
        return text.Any(char.IsLetter)&&(ArabicGameNames.Speaker(text) is null||SubtitleStabilizer.EndsSentence(text));
    }
    /// <summary>Some OCR engines split a coloured speaker label from its white text. Join only adjacent geometry.</summary>
    public static Reading AttachStandaloneLabels(Reading reading)
    {
        var lines=reading.Lines.ToList();var result=new List<TextLine>();
        for(int i=0;i<lines.Count;i++){
            var line=lines[i];var labelText=line.Text.Trim();if(ArabicGameNames.Speaker(labelText) is not null)labelText+=":";var match=Label().Match(labelText);
            if(!match.Success||labelText[match.Length..].Any(char.IsLetter)){result.Add(line);continue;}
            if(i+1>=lines.Count)continue;
            var next=lines[i+1];bool adjacent=false;
            if(line.Box is {} a&&next.Box is {} b){var h=Math.Max(a.Bottom-a.Top,b.Bottom-b.Top);adjacent=Math.Abs(a.Top-b.Top)<h*.6||b.Top>=a.Top&&b.Top-a.Bottom<h*1.3&&Math.Min(a.Right,b.Right)>Math.Max(a.Left,b.Left);}
            if(!adjacent)continue;
            result.Add(next with{Text=match.Groups["name"].Value+": "+next.Text.Trim(),Box=line.Box!.Value.Union(next.Box!.Value)});i++;
        }
        return new Reading(result);
    }

    /// <summary>İki okuma aynı söz mü: sözcüklerin çoğu ortak (gürültülü okumada birkaç sözcük bozuk olabilir).</summary>
    private static bool SameWords(string noisy, string clean)
    {
        var a = TextMatch.Normalize(noisy).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var b = TextMatch.Normalize(clean).Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        if (a.Length == 0 || b.Count == 0) return false;
        if (TextMatch.IsSimilar(string.Join(' ', a), string.Join(' ', b), 0.7)) return true;
        var shared = a.Count(word => b.Remove(word));
        return shared >= 2 && shared * 2 >= Math.Min(a.Length, shared + b.Count);
    }

    [GeneratedRegex(@"^(?<name>\p{Lu}[\p{L}.’'-]+(?: \p{Lu}[\p{L}.’'-]+){0,2}):(?=\s|$)")]
    private static partial Regex Label();
}
