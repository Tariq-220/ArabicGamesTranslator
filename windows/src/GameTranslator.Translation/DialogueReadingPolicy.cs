// SPDX-License-Identifier: AGPL-3.0-only
using GameTranslator.Core;
namespace GameTranslator.Translation;
public static class DialogueReadingPolicy {
 public static void Configure(SubtitlePipeline pipeline,bool latestOnly,string mode){
  // Preserve pre-menu dialogue timing: no mandatory extra wait or immediate offscreen drop.
  pipeline.MinimumStableMs=0;pipeline.DropOffscreenResults=false;
  pipeline.PendingLineLimit=latestOnly?1:SubtitlePipeline.MaxWaiting;
  pipeline.DialogueBlockOnly=true; // Interface groups have their own bounded, transient lane.
  // Pack-specific fuzzy holding must not gate ordinary Arabic dialogue: slang/names may look misspelled.
  pipeline.HoldWithoutPacks=false;
  pipeline.CleanWait=120;pipeline.PackWait=240;
 }
}
