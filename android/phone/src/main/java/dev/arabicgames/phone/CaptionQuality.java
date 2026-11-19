// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames.phone;
import java.util.*;import java.util.regex.*;import java.text.BreakIterator;
/** Bounded, deterministic validation and segmentation. Never guesses missing dialogue. */
final class CaptionQuality {
 static boolean objective(String text){return text.trim().matches("(?i)(?:Follow (?:Lucia|Brian|the manager)|FoUow Lucia|Find Donnie|Rescue Donnie again|Untie Donnie|Escape with Donnie|Hold o(?:ff|ft) the F(?:i)?nal Chapter|Grab a bike and catch up to Donnie|Help build the coffee table|Pick up the (?:glass|qlass) top|Search the camp for Donnie|Find a way into the shack|Save Donnie|Head for the plane|Go to the plane|Get in the sea plane|Escape the camp|Fly back to Brian.s marina)");}
 static java.util.List<String> sentences(String text){
  String flat=text.replaceAll("\\s+"," ").trim();BreakIterator breaks=BreakIterator.getSentenceInstance(Locale.US);breaks.setText(flat);List<String> parts=new ArrayList<>();int start=breaks.first(),end;
  while((end=breaks.next())!=BreakIterator.DONE){String part=flat.substring(start,end).trim();if(!part.isEmpty())parts.add(part);start=end;}
  if(parts.isEmpty())return Collections.singletonList(flat);
  // Short whole captions stay a single inference. Split multi-sentence captions whose tail was being omitted.
  if(parts.size()<2||flat.length()<70)return Collections.singletonList(flat);
  if(parts.size()>4){String rest=String.join(" ",parts.subList(3,parts.size()));parts=new ArrayList<>(parts.subList(0,3));parts.add(rest);}return parts;
 }
 private static List<String> words(String text){List<String> a=new ArrayList<>();Matcher m=Pattern.compile("[A-Za-z][A-Za-z’']*").matcher(text);while(m.find())if(m.group().length()>=2)a.add(m.group());return a;}
 static boolean reject(String source,String result){
  List<String> out=words(result),input=words(source);if(out.isEmpty())return result.trim().isEmpty();
  boolean arabic=Pattern.compile("[\\u0600-\\u06ff]").matcher(result).find();
  Set<String> common=new HashSet<>(Arrays.asList("a an and the i we you he she they it hello hi yeah yes no thanks okay good come go follow leave now".split(" ")));
  if(!arabic&&source.trim().equals(result.trim())&&out.size()<=3&&out.stream().allMatch(w->Character.isUpperCase(w.charAt(0))&&!common.contains(w.toLowerCase(Locale.ROOT))&&!w.contains("'")))return false;
  for(String word:out){
   if(word.equals(word.toUpperCase(Locale.ROOT))&&word.length()<=6&&input.contains(word))continue;
   if(arabic&&!Arrays.asList("ouch","pfft","hmm","hello","okay","thanks").contains(word.toLowerCase(Locale.ROOT))&&(Arrays.asList("iPhone","PlayStation","Xbox").contains(word)||Character.isUpperCase(word.charAt(0))&&(Pattern.compile("(?:^|\\n)\\s*"+Pattern.quote(word)+"\\s*:").matcher(source).find()||input.indexOf(word)>0||input.size()==1&&input.get(0).equals(word))))continue;
   return true;
  }return false;
 }
}
