// SPDX-License-Identifier: AGPL-3.0-only
using GameTranslator.Core;
namespace GameTranslator.Translation;
public static class DialogueReadingPolicy {
 public static void Configure(SubtitlePipeline pipeline,bool latestOnly,string mode){
  // Preserve pre-menu dialogue timing: no mandatory extra wait or immediate offscreen drop.
  pipeline.MinimumStableMs=0;pipeline.DropOffscreenResults=false;
  pipeline.PendingLineLimit=latestOnly?1:SubtitlePipeline.MaxWaiting;
  pipeline.DialogueBlockOnly=ReadingModes.DialogueOnly(mode);
  // Coalesce incomplete OCR rereads before they flood the translator, while complete sentences stay immediate.
  pipeline.HoldWithoutPacks=pipeline.DialogueBlockOnly;
  pipeline.CleanWait=120;pipeline.PackWait=240;
 }
}
