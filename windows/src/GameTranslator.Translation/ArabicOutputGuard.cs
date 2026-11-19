// SPDX-License-Identifier: AGPL-3.0-only
using System.Text.RegularExpressions;
namespace GameTranslator.Translation;
/// <summary>Reject untranslated prose, while retaining speaker names, acronyms and product names.</summary>
public static class ArabicOutputGuard {
 private static readonly HashSet<string> Acronyms=new("GTA VI V FBI FIB CIA SWAT GPS RPG SMS PS5 PS4 USB TV NPC ATM CEO".Split(' '),StringComparer.Ordinal);
 public static string PrepareCaption(string source,string result){
  if(!Regex.IsMatch(result,@"\p{IsArabic}"))return result;
  var cleaned=Regex.Replace(result,@"[A-Za-z][A-Za-z0-9’']*(?:-[A-Za-z0-9]+)*",match=>{
   string word=match.Value;
   return IsName(source,word)||Acronyms.Contains(word)?word:"[غير واضح]";
  });
  return Regex.Replace(cleaned,@"(?:\[غير واضح\][ \t]*){2,}","[غير واضح] ").TrimEnd();
 }
 public static bool RejectUnusable(string source,string result){
  if(string.IsNullOrWhiteSpace(result)||Degenerate(result))return true;
  if(!Reject(source,result))return false;
  // A residual name/word is a quality defect, not a reason to hide the entire Arabic caption.
  int arabic=result.Count(c=>char.IsLetter(c)&&c>='\u0600'&&c<='\u06ff');
  int latin=result.Count(c=>c is >= 'a' and <= 'z' or >= 'A' and <= 'Z');
  return arabic==0||latin>=arabic;
 }
 private static bool Degenerate(string result)=>Regex.IsMatch(result,@"(?i)(?:\b([a-z])\b[\s.,•]*)\1(?:[\s.,•]+\1){4,}")||Regex.IsMatch(result,@"(?:^|\s)([\p{L}]{2,})(?:\s+\1){5,}(?:\s|$)");
 public static bool Reject(string source,string result){
  // Degenerate decoding can repeat single letters, which the prose-token check below excludes.
  if(Degenerate(result))return true;
  var words=Regex.Matches(result,@"[A-Za-z][A-Za-z’']*").Select(m=>m.Value).Where(w=>w.Length>=2).ToArray();
  if(words.Length==0)return false;
  if(!Regex.IsMatch(result,@"\p{IsArabic}"))return !(words.All(w=>Acronyms.Contains(w)&&Regex.Matches(source,@"[A-Za-z][A-Za-z’']*").Any(m=>m.Value==w))||OnlyProperName(source,result,words));
  return words.Any(w=>!(Acronyms.Contains(w)&&Regex.Matches(source,@"[A-Za-z][A-Za-z’']*").Any(m=>m.Value==w))&&!IsName(source,w));
 }
 private static bool OnlyProperName(string source,string result,string[] words){
  if(words.Length>3||source.Trim()!=result.Trim())return false;
  var common=new HashSet<string>("a an and the i we you he she they it hello hi yeah yes no thanks okay good come go follow leave now".Split(' '),StringComparer.OrdinalIgnoreCase);
  return words.All(w=>char.IsUpper(w[0])&&!w.All(char.IsUpper)&&!common.Contains(w)&&!w.Contains('\''));
 }
 private static bool IsName(string source,string word){
  if(new[]{"ouch","pfft","hmm","hello","okay","thanks"}.Contains(word.ToLowerInvariant()))return false;
  if(word is "iPhone" or "PlayStation" or "Xbox")return true;
  if(!char.IsUpper(word[0])||word.Length<2)return false;
  if(Regex.IsMatch(source,@"(?:^|\n)\s*"+Regex.Escape(word)+@"\s*:"))return true;
  if(Regex.IsMatch(word,@"^[A-Z]-[A-Z][a-z]+$")&&source.Contains(word,StringComparison.Ordinal))return true;
  if(word.All(char.IsUpper))return false;
  // A repeated capitalized proper noun is safe; an isolated sentence's first word is not enough.
  var matches=Regex.Matches(source,@"[A-Za-z][A-Za-z’']*").Select(m=>m.Value).ToArray();
  return matches.Skip(1).Any(w=>w==word)||matches.Length==1&&matches[0]==word;
 }
}
