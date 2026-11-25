// SPDX-License-Identifier: AGPL-3.0-only
using GameTranslator.Core;
namespace GameTranslator.Translation;
public static class DialogueReadingPolicy {
 public static void Configure(SubtitlePipeline pipeline,bool latestOnly,string mode,bool faster=false){
  // VI uses a short time-based settle window; sentence punctuation is not proof that a fading frame is complete.
  pipeline.MinimumStableMs=faster?80:120;pipeline.MinimumDisplayMs=faster?450:700;pipeline.DropOffscreenResults=false;
  // Keep short consecutive replies even in the former latest-only preset. The queue remains bounded.
  pipeline.PendingLineLimit=SubtitlePipeline.MaxWaiting;
  pipeline.KeepRapidReplies=true;pipeline.ExpireWhenSourceGone=true;
  pipeline.DialogueBlockOnly=ReadingModes.DialogueOnly(mode);
 }
}
