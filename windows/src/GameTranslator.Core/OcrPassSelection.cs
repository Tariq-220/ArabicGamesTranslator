// SPDX-License-Identifier: AGPL-3.0-only
namespace GameTranslator.Core;
/// <summary>Prefer the original pixels when readable; a white mask can turn scenery into longer fake text.</summary>
public static class OcrPassSelection {
 public static Reading Choose(Reading original,Reading white){
  if(string.IsNullOrWhiteSpace(original.Text))return white;
  if(string.IsNullOrWhiteSpace(white.Text))return original;
  // A readable original must not lose to a mask merely because the mask produced more letters.
  if(SubtitleStabilizer.LooksClean(original.Text)&&!OcrNoise.LooksGarbled(original.Text))return original;
  return OcrNoise.Score(white.Text)>OcrNoise.Score(original.Text)?SpeakerMerge.WithSpeakerFrom(white,original):SpeakerMerge.WithSpeakerFrom(original,white);
 }
}
