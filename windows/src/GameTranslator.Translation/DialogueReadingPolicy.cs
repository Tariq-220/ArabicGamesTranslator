// SPDX-License-Identifier: AGPL-3.0-only
using GameTranslator.Core;
namespace GameTranslator.Translation;
public static class DialogueReadingPolicy {
 public static void Configure(SubtitlePipeline pipeline,bool latestOnly,string mode){
  // VI uses a short time-based settle window; sentence punctuation is not proof that a fading frame is complete.
  pipeline.MinimumStableMs=120;pipeline.MinimumDisplayMs=700;pipeline.DropOffscreenResults=false;
  pipeline.PendingLineLimit=latestOnly?1:SubtitlePipeline.MaxWaiting;
  pipeline.DialogueBlockOnly=ReadingModes.DialogueOnly(mode);
 }
}
