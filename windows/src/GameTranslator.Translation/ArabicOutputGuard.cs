// SPDX-License-Identifier: AGPL-3.0-only
using System.Text.RegularExpressions;
namespace GameTranslator.Translation;
/// <summary>Reject untranslated prose, while retaining speaker names, acronyms and product names.</summary>
public static class ArabicOutputGuard {
 public static bool Reject(string source,string result){
  var words=Regex.Matches(result,@"[A-Za-z][A-Za-z’']*").Select(m=>m.Value).Where(w=>w.Length>=2).ToArray();
  if(words.Length==0)return false;
  if(!Regex.IsMatch(result,@"\p{IsArabic}"))return !(words.All(w=>w.All(char.IsUpper)&&w.Length<=6)||OnlyProperName(source,result,words));
  return words.Any(w=>!w.All(char.IsUpper)&&!IsName(source,w));
 }
 private static bool OnlyProperName(string source,string result,string[] words){
  if(words.Length>3||source.Trim()!=result.Trim())return false;
  var common=new HashSet<string>("a an and the i we you he she they it hello hi yeah yes no thanks okay good come go follow leave now".Split(' '),StringComparer.OrdinalIgnoreCase);
  return words.All(w=>char.IsUpper(w[0])&&!common.Contains(w)&&!w.Contains('\''));
 }
 private static bool IsName(string source,string word){
  if(word is "iPhone" or "PlayStation" or "Xbox")return true;
  if(!char.IsUpper(word[0])||word.Length<2)return false;
  if(Regex.IsMatch(source,@"(?:^|\n)\s*"+Regex.Escape(word)+@"\s*:"))return true;
  // A repeated capitalized proper noun is safe; an isolated sentence's first word is not enough.
  var matches=Regex.Matches(source,@"[A-Za-z][A-Za-z’']*").Select(m=>m.Value).ToArray();
  return matches.Skip(1).Any(w=>w==word)||matches.Length==1&&matches[0]==word;
 }
}
