// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames.phone;
/** RDR2-only corrections reviewed against the user's comparison log. */
final class DialogueProfile {
 static boolean rdr2(String game){return game!=null&&(game.trim().equalsIgnoreCase("RDR2")||game.trim().equalsIgnoreCase("Red Dead Redemption 2"));}
 static boolean interactionPrompt(String text,float center){if(center<.70f)return false;String words=text.toLowerCase(java.util.Locale.ROOT).replaceAll("[^a-z ]"," ").trim().replaceAll("\\s+"," ");if(!words.matches(".*\\b(greet|antagonize|rob)\\b.*"))return false;for(String word:words.split(" "))if(!java.util.Arrays.asList("greet","antagonize","rob","a","b","x","y","lb","rb","lt","rt").contains(word))return false;return true;}
 static String english(String text,String game){if(!java.util.regex.Pattern.compile("[A-Za-z]{2}").matcher(text).find()&&text.chars().filter(c->Character.isLetter(c)&&c>127).count()>=2&&java.util.regex.Pattern.compile("\\p{L}[._-]\\p{L}").matcher(text).find())return "";if(!rdr2(game))return text;switch(text.trim().toLowerCase(java.util.Locale.ROOT)){
  case "itrank you...":return "Thank you...";case "vii hold them off.":return "I'll hold them off.";case "rm coming with you.":return "I'm coming with you.";case "1 did.":return "I did.";default:return text.replaceAll("\\bAnhur\\b","Arthur");}}
 static String arabic(String source,String arabic,String game){if(!rdr2(game))return arabic;
  if(java.util.regex.Pattern.compile("\\bDutch\\b").matcher(source).find())arabic=arabic.replace("الهولندية","داتش").replace("الهولندي","داتش");
  if(java.util.regex.Pattern.compile("\\bArthur\\b").matcher(source).find())arabic=arabic.replace("آرتر","آرثر");
  if(source.toLowerCase(java.util.Locale.ROOT).contains("copperhead landing"))arabic=arabic.replace("Copperhead الهبوط","كوبرهيد لاندينغ").replace("كوبرهيد الهبوط","كوبرهيد لاندينغ");return arabic;
 }
}
