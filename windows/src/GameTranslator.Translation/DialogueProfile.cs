// SPDX-License-Identifier: AGPL-3.0-only
using System.Text.RegularExpressions;
using GameTranslator.Core;
namespace GameTranslator.Translation;
/// <summary>Conservative RDR2-only corrections reviewed from the supplied comparison log.</summary>
public static class DialogueProfile {
 public static bool Rdr2(string game)=>game.Trim().Equals("RDR2",StringComparison.OrdinalIgnoreCase)||game.Trim().Equals("Red Dead Redemption 2",StringComparison.OrdinalIgnoreCase);
 public static string English(string text,string game){
  if(!Rdr2(game))return text;
  var fixes=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase){["Itrank you..."]="Thank you...",["VII hold them off."]="I'll hold them off.",["rm coming with you."]="I'm coming with you.",["1 did."]="I did."};
  if(fixes.TryGetValue(text.Trim(),out var value))return value;
  return Regex.Replace(text,@"\bAnhur\b","Arthur");
 }
 public static string Arabic(string source,string arabic,string game){
  if(!Rdr2(game))return arabic;
  if(Regex.IsMatch(source,@"\bDutch\b"))arabic=Regex.Replace(arabic,"الهولندية|الهولندي","داتش");
  if(Regex.IsMatch(source,@"\bArthur\b"))arabic=arabic.Replace("آرثر","آرثر").Replace("آرتر","آرثر");
  if(source.Contains("Copperhead Landing",StringComparison.OrdinalIgnoreCase))arabic=arabic.Replace("Copperhead الهبوط","كوبرهيد لاندينغ").Replace("كوبرهيد الهبوط","كوبرهيد لاندينغ");
  return arabic;
 }
}
public sealed class ProfileTranslator(ITextTranslator inner,string game):ITextTranslator {
 public async Task<string> TranslateAsync(string text,CancellationToken token=default)=>DialogueProfile.Arabic(text,await inner.TranslateAsync(text,token),game);
 public bool Prefetches=>inner.Prefetches;
 public void Prefetch(string text)=>inner.Prefetch(text);
 public IReadOnlyList<GameTranslator.Core.Dubbing.SpokenLine> SpokenLines(string text)=>inner.SpokenLines(text);
}
