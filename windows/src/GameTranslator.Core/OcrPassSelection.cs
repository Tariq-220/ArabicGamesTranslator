// SPDX-License-Identifier: AGPL-3.0-only
namespace GameTranslator.Core;
/// <summary>Prefer the original pixels when readable; a white mask can turn scenery into longer fake text.</summary>
public static class OcrPassSelection {
 public static Reading Choose(Reading original,Reading white){
  if(string.IsNullOrWhiteSpace(original.Text))return white;
  if(string.IsNullOrWhiteSpace(white.Text))return original;
  if(Extends(original.Text,white.Text))return white;
  // A readable original must not lose to a mask merely because the mask produced more letters.
  if(SubtitleStabilizer.LooksClean(original.Text)&&!OcrNoise.LooksGarbled(original.Text))return original;
  return OcrNoise.Score(white.Text)>OcrNoise.Score(original.Text)?SpeakerMerge.WithSpeakerFrom(white,original):SpeakerMerge.WithSpeakerFrom(original,white);
 }
 private static bool Extends(string shortText,string fullText){
  if(!SubtitleStabilizer.LooksClean(fullText)||OcrNoise.LooksGarbled(fullText))return false;
  var small=TextMatch.Normalize(shortText).Split(' ',StringSplitOptions.RemoveEmptyEntries);
  var full=TextMatch.Normalize(fullText).Split(' ',StringSplitOptions.RemoveEmptyEntries);
  if(small.Length==0||full.Length<=small.Length)return false;
  if(small.Length==1&&full.Length<4)return false;
  return full.Take(small.Length).SequenceEqual(small)||full.TakeLast(small.Length).SequenceEqual(small);
 }

}
