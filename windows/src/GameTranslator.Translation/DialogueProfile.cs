// SPDX-License-Identifier: AGPL-3.0-only
using System.Text.RegularExpressions;
using GameTranslator.Core;
namespace GameTranslator.Translation;
/// <summary>Conservative RDR2-only corrections reviewed from the supplied comparison log.</summary>
public static class DialogueProfile {
 public static bool IsInterfaceText(string text)=>Regex.IsMatch(text,@"SessionState|\bStreaming[.,]?\s+Reason\b|^Ready\s+.{0,12}[{|=]|^ms\s|^Comparison\s*[-–]|^Compa\s+iso\s*[-–]",RegexOptions.IgnoreCase)
  ||(!Regex.IsMatch(text,@"[.!?]")&&Regex.IsMatch(text,@"^PHOTO\b",RegexOptions.IgnoreCase)&&Regex.IsMatch(text,@"\b(?:SELECT|BACK|BACH|MODE)\b",RegexOptions.IgnoreCase))
  ||Regex.IsMatch(text,@"^(?:[O0]\s+)?(?:RETRY\s*CHECKPOINT|CANCEL MISSION)(?:\s+[O0])?(?:\s+CANCEL MISSION\s*[O0]?)?$",RegexOptions.IgnoreCase);

 public static bool Rdr2(string game)=>game.Trim().Equals("RDR2",StringComparison.OrdinalIgnoreCase)||game.Trim().Equals("Red Dead Redemption 2",StringComparison.OrdinalIgnoreCase);
 public static Reading DialogueOnly(Reading reading,TextBox area,string game){
  reading=new Reading(reading.Lines.Where(line=>!IsInterfaceText(line.Text)));
  if(!Rdr2(game))return reading;
  return new Reading(reading.Lines.Where(line=>{
   if(line.Box is not {} relative)return true;
   var box=relative.PlacedIn(area);
   if((box.Left+box.Right)/2<.70)return true;
   var words=Regex.Matches(line.Text,"[A-Za-z]+").Select(m=>m.Value.ToLowerInvariant()).ToArray();
   var prompts=new HashSet<string>{"greet","antagonize","rob","a","b","x","y","lb","rb","lt","rt"};
   // Only the known interaction labels at the right edge; dialogue containing these words survives.
   return !words.Any(w=>w is "greet" or "antagonize" or "rob")||!words.All(prompts.Contains);
  }));
 }
 public static string English(string text,string game){
  if(!Rdr2(game))return text;
  var fixes=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase){["Itrank you..."]="Thank you...",["VII hold them off."]="I'll hold them off.",["rm coming with you."]="I'm coming with you.",["1 did."]="I did.",["1 do."]="I do."};
  if(fixes.TryGetValue(text.Trim(),out var value))return value;
  text=Regex.Replace(text,@"(?<=[A-Za-z])•(?=[A-Za-z])", "'");
  text=Regex.Replace(text,@"(?<=[A-Za-z.!?])„", "");
  return Regex.Replace(text,@"\bAnhur\b","Arthur");
 }
 public static string? Reviewed(string source,string game) {
  if(!Rdr2(game))return null;
  // These are explicitly reviewed phrases, not claims of improved model inference.
  var phrases=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase) {
   ["Mount your horse"]="اركب حصانك.",
   ["Here, be careful with the china, sir..."]="انتبه إلى الأواني الخزفية يا سيدي...",
   ["they are philistines in that area..."]="إنهم يفتقرون إلى الذوق في هذا المجال...",
   ["right now I have a couple of commissions."]="لدي حاليًا طلبان لتنفيذ أعمال فنية.",
   ["I need at least fifteen egret plumes..."]="أحتاج إلى خمس عشرة ريشة على الأقل من طائر البلشون...",
   ["fifteen assorted orchids."]="خمس عشرة زهرة أوركيد متنوعة.",
   ["I told you you two would get on."]="قلت لك إنكما ستنسجمان.",
   ["Swanson, well, Swanson's actually gone and cleaned up his act."]="أما سوانسون فقد أصلح حاله فعلًا."
  };
  return phrases.GetValueOrDefault(source.Trim());
 }
 public static string Arabic(string source,string arabic,string game){
  if(!Rdr2(game))return arabic;
  if(Regex.IsMatch(source,@"\bDutch\b",RegexOptions.IgnoreCase)&&!Regex.IsMatch(source,@"\b(?:speak|speaking|learn|learning)\s+(?:some\s+)?Dutch\b|\bthe Dutch\b|\bDutch language\b",RegexOptions.IgnoreCase)) {
   arabic=Regex.Replace(arabic,@"للهولندي(?:ة)?","لداتش");
   arabic=Regex.Replace(arabic,@"الهولندي(?:ة)?","داتش");
  }
  if(Regex.IsMatch(source,@"\bArthur\b"))arabic=arabic.Replace("آرثر","آرثر").Replace("آرتر","آرثر");
  if(source.Contains("Copperhead Landing",StringComparison.OrdinalIgnoreCase))arabic=arabic.Replace("Copperhead الهبوط","كوبرهيد لاندينغ").Replace("كوبرهيد الهبوط","كوبرهيد لاندينغ");
  return arabic;
 }
}
public sealed class ProfileTranslator(ITextTranslator inner,string game):ITextTranslator {
 public async Task<string> TranslateAsync(string text,CancellationToken token=default)=>CommunityCorrections.Improve(game,text,DialogueProfile.Arabic(text,await inner.TranslateAsync(text,token),game));
 public bool Prefetches=>inner.Prefetches;
 public void Prefetch(string text)=>inner.Prefetch(text);
 public IReadOnlyList<GameTranslator.Core.Dubbing.SpokenLine> SpokenLines(string text)=>inner.SpokenLines(text);
}
