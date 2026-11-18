// SPDX-License-Identifier: AGPL-3.0-only
using GameTranslator.Core;
namespace GameTranslator.Translation;
public static class ReadingModes {
 public static CropRegion Region(string mode)=>mode switch{"menus"=>new(.08,.97,0,1),"phone-right"=>new(.08,.97,.55,.99),"phone-left"=>new(.08,.97,.01,.45),_=>new(.62,.97,.10,.90)};
 public static bool DialogueOnly(string mode)=>mode=="dialogue";
}
