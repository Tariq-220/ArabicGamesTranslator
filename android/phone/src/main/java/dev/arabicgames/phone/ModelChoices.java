package dev.arabicgames.phone;
final class ModelChoices {
 static String[] forProvider(String p){
  if(AiPreferences.GEMINI.equals(p))return new String[]{"gemini-3.5-flash-lite","gemini-3.8-flash","gemini-3.6-flash","gemini-3.1-flash-lite"};
  if(AiPreferences.OPENAI.equals(p))return new String[]{"gpt-6-luna","gpt-6.1-terra","gpt-5.4-mini","gpt-4o-mini"};
  if(AiPreferences.LAN.equals(p))return new String[]{"quickmt","jisr-msa","jisr-saudi","hy-mt"};
  if(AiPreferences.DEEPL.equals(p))return new String[]{"auto","latency_optimized","prefer_quality_optimized"};
  if(AiPreferences.GOOGLE.equals(p))return new String[]{"google-gtx"};
  return new String[]{"local"};
 }
 static boolean customAllowed(String p){return AiPreferences.GEMINI.equals(p)||AiPreferences.OPENAI.equals(p);}
}
