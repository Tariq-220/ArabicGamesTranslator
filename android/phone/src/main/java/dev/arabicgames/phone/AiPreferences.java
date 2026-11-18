// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames.phone;
import android.content.*;
import android.security.keystore.*;
import android.util.Base64;
import java.security.KeyStore;
import javax.crypto.*;
import javax.crypto.spec.GCMParameterSpec;

/** Private, encrypted provider keys. No keys in URLs, logs, exports or relay messages. */
final class AiPreferences {
 static final String NATIVE="ct2-native";
 static final String GOOGLE="google-gtx";
 static final String LOCAL="local",GEMINI="gemini",OPENAI="openai",DEEPL="deepl",LAN="lan";
 private static volatile Config session;
 static SharedPreferences prefs(Context c){return c.getSharedPreferences("translation",Context.MODE_PRIVATE);}
 static String defaultModel(String provider){return NATIVE.equals(provider)?"quickmt-en-ar":LOCAL.equals(provider)?"local":GOOGLE.equals(provider)?"google-gtx":LAN.equals(provider)?"quickmt":DEEPL.equals(provider)?"auto":OPENAI.equals(provider)?"gpt-5.4-mini":"gemini-3.5-flash-lite";}
 static String label(String provider){return NATIVE.equals(provider)?"محلي على الجوال":GOOGLE.equals(provider)?"Google — بدون مفتاح":LAN.equals(provider)?"نموذج على الكمبيوتر":DEEPL.equals(provider)?"DeepL":OPENAI.equals(provider)?"OpenAI":GEMINI.equals(provider)?"Gemini":"محلي";}
 static String provider(Context c){Config active=session;return active!=null?active.provider:prefs(c).getString("provider",LOCAL);}
 static boolean cloud(Context c){Config active=session;return active!=null?active.cloud():!NATIVE.equals(provider(c))&&!LOCAL.equals(provider(c))&&prefs(c).getBoolean("consent",false);}
 static boolean sessionOnly(String provider){Config active=session;return active!=null&&active.provider.equals(provider);}
 static void clearSession(){session=null;}
 static void useForSession(String provider,String model,String game,String typed,boolean consent,boolean fast){
  useForSession(provider,model,game,typed,consent,fast,"");
 }
 static void useForSession(String provider,String model,String game,String typed,boolean consent,boolean fast,String endpoint){
  String value=validateInput(provider,model,typed,consent);Config previous=session;
  if(value.isEmpty()&&previous!=null&&previous.provider.equals(provider))value=previous.key;
  if(value.isEmpty()&&!GOOGLE.equals(provider)&&!LOCAL.equals(provider)&&!NATIVE.equals(provider))throw new IllegalArgumentException("الصق المفتاح لهذه الجلسة؛ هذا الخيار لا يقرأ التخزين المشفّر.");
  session=new Config(provider,model,game,value,consent,fast,endpoint);
 }
 static boolean canTranslate(Context c){return NATIVE.equals(provider(c))?LocalModels.runtime()&&LocalModels.installed(c,LocalModels.selected(c)):Status.modelReady||cloud(c);}
 static String game(Context c){Config active=session;return active!=null?active.game:prefs(c).getString("game","Red Dead Redemption 2");}
 static final class Config {
  final String provider,model,game,key,endpoint;final boolean consent,fast;
  Config(String p,String m,String g,String k,boolean approved){this(p,m,g,k,approved,false);}
  Config(String p,String m,String g,String k,boolean approved,boolean faster){this(p,m,g,k,approved,faster,"");}
  Config(String p,String m,String g,String k,boolean approved,boolean faster,String address){provider=p;model=m;game=g;key=k;consent=approved;fast=faster;endpoint=address;}
  boolean cloud(){return !NATIVE.equals(provider)&&!LOCAL.equals(provider)&&consent;}
  String label(){return AiPreferences.label(provider)+" / "+model+(fast&&GEMINI.equals(provider)?" · وضع أسرع":"");}
 }
 static Config read(Context c)throws Exception{
  Config active=session;if(active!=null)return active;
  SharedPreferences p=prefs(c);String provider=p.getString("provider",LOCAL);
  return new Config(provider,p.getString(provider+".model",defaultModel(provider)),p.getString("game","Red Dead Redemption 2"),(LOCAL.equals(provider)||NATIVE.equals(provider))?"":key(c,provider),p.getBoolean("consent",false),p.getBoolean("geminiFast",false),p.getString("lanAddress",""));
 }
 private static SecretKey storageKey()throws Exception{
  KeyStore store=KeyStore.getInstance("AndroidKeyStore");store.load(null);
  if(store.containsAlias("ArabicGamesAiKeys"))return (SecretKey)store.getKey("ArabicGamesAiKeys",null);
  KeyGenerator generator=KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES,"AndroidKeyStore");
  generator.init(new KeyGenParameterSpec.Builder("ArabicGamesAiKeys",KeyProperties.PURPOSE_ENCRYPT|KeyProperties.PURPOSE_DECRYPT).setBlockModes(KeyProperties.BLOCK_MODE_GCM).setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE).build());
  return generator.generateKey();
 }
 static String key(Context c,String provider)throws Exception{
  String encrypted=prefs(c).getString(provider+".key","");if(encrypted.isEmpty())return "";
  String[] parts=encrypted.split(":",2);Cipher cipher=Cipher.getInstance("AES/GCM/NoPadding");
  cipher.init(Cipher.DECRYPT_MODE,storageKey(),new GCMParameterSpec(128,Base64.decode(parts[0],Base64.NO_WRAP)));
  return new String(cipher.doFinal(Base64.decode(parts[1],Base64.NO_WRAP)),java.nio.charset.StandardCharsets.UTF_8);
 }
 static String normalizedKey(String value){return value.replace("\ufeff","").replace("\u200b","").trim();}
 static String validateInput(String provider,String model,String newKey,boolean consent){
  String value=normalizedKey(newKey);
  if(NATIVE.equals(provider)){if(!java.util.Arrays.asList("quickmt-en-ar","jisr-msa","jisr-saudi").contains(model))throw new IllegalArgumentException("اختر نموذجًا محليًا من القائمة.");return "";}
  if(LOCAL.equals(provider))return value;
  if(!GEMINI.equals(provider)&&!OPENAI.equals(provider)&&!DEEPL.equals(provider)&&!LAN.equals(provider)&&!GOOGLE.equals(provider)&&!LOCAL.equals(provider)&&!NATIVE.equals(provider))throw new IllegalArgumentException("اختر محرك ترجمة من القائمة.");
  if(!consent)throw new IllegalArgumentException("فعّل الموافقة على إرسال نص الحوار أسفل خانة المفتاح، ثم احفظ.");
  if(!model.matches("[a-zA-Z0-9._-]{1,100}"))throw new IllegalArgumentException("اسم النموذج غير صالح. أعد اختيار الخدمة لاستعادة الاسم الافتراضي.");
  if(value.length()>1024||value.matches("(?s).*\\s.*"))throw new IllegalArgumentException("الصق المفتاح وحده؛ يوجد فراغ أو سطر داخل المفتاح.");
  return value;
 }
 static void save(Context c,String provider,String model,String game,String newKey,boolean consent)throws Exception{
  String value=validateInput(provider,model,newKey,consent),encrypted=null;
  if(!NATIVE.equals(provider)&&!LOCAL.equals(provider)&&!GOOGLE.equals(provider)&&value.isEmpty()&&key(c,provider).isEmpty())throw new IllegalArgumentException("خانة المفتاح فارغة ولا يوجد مفتاح محفوظ لهذه الخدمة. الصقه ثم احفظ.");
  if(!NATIVE.equals(provider)&&!LOCAL.equals(provider)&&!GOOGLE.equals(provider)&&!value.isEmpty()){
   Cipher cipher=Cipher.getInstance("AES/GCM/NoPadding");cipher.init(Cipher.ENCRYPT_MODE,storageKey());
   encrypted=Base64.encodeToString(cipher.getIV(),Base64.NO_WRAP)+":"+Base64.encodeToString(cipher.doFinal(value.getBytes(java.nio.charset.StandardCharsets.UTF_8)),Base64.NO_WRAP);
  }
  SharedPreferences.Editor edit=prefs(c).edit().putString("provider",provider).putString("game",game.trim()).putBoolean("consent",!NATIVE.equals(provider)&&!LOCAL.equals(provider)&&consent);
  if(!LOCAL.equals(provider))edit.putString(provider+".model",model);
  if(encrypted!=null)edit.putString(provider+".key",encrypted);
  if(!edit.commit())throw new java.io.IOException("Storage");
  clearSession();
 }
 static void clear(Context c){clearSession();prefs(c).edit().clear().apply();}
}
