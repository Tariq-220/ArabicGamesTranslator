// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames.phone;
import java.io.*;
import java.net.*;
import java.nio.charset.StandardCharsets;
import java.util.List;

final class AiClient {
 static final class Failure extends Exception {
  Failure(String safeMessage){super(safeMessage);}
 }
 static String translate(AiPreferences.Config config,String source,List<String> context)throws Failure{
  if(AiPreferences.GOOGLE.equals(config.provider)){if(!config.cloud())throw new Failure("وافق على إرسال نص الحوار إلى Google أولًا.");if(source.length()>1200)throw new Failure("ضيّق منطقة القراءة.");return GoogleGtx.translate(source);}
  if(!config.cloud()||config.key.isEmpty())throw new Failure("اختر المحرك وأدخل مفتاحه ووافق على إرسال نص الحوار.");
  if(source.length()>1200)throw new Failure("النص المقروء طويل جدًا. اضبط منطقة الترجمة بدل القوائم.");
  try{return send(config,source,context,endpoint(config));}catch(IllegalArgumentException e){throw new Failure("عنوان خادم الكمبيوتر غير صالح. راجع العنوان المحلي والمنفذ.");}
 }
 static String endpoint(AiPreferences.Config config){
  if(AiPreferences.LAN.equals(config.provider))return LocalEndpoint.validate(config.endpoint)+"/translate";
  if(AiPreferences.DEEPL.equals(config.provider))return config.key.endsWith(":fx")?"https://api-free.deepl.com/v2/translate":"https://api.deepl.com/v2/translate";
  return AiPreferences.OPENAI.equals(config.provider)?"https://api.openai.com/v1/chat/completions":"https://generativelanguage.googleapis.com/v1beta/models/"+config.model+":generateContent";
 }
 // Endpoint injection is package-private for local HTTP tests; UI only exposes official providers.
 static String send(AiPreferences.Config config,String source,List<String> context,String endpoint)throws Failure{
  HttpURLConnection connection=null;
  try{
   connection=(HttpURLConnection)new URL(endpoint).openConnection();connection.setInstanceFollowRedirects(false);
   connection.setConnectTimeout(3000);connection.setReadTimeout(5000);connection.setRequestMethod("POST");connection.setDoOutput(true);
   connection.setRequestProperty("Content-Type","application/json; charset=utf-8");
   if(AiPreferences.DEEPL.equals(config.provider))connection.setRequestProperty("Authorization","DeepL-Auth-Key "+config.key);
   else connection.setRequestProperty(AiPreferences.OPENAI.equals(config.provider)||AiPreferences.LAN.equals(config.provider)?"Authorization":"x-goog-api-key",AiPreferences.OPENAI.equals(config.provider)||AiPreferences.LAN.equals(config.provider)?"Bearer "+config.key:config.key);
   byte[] body=AiRequest.body(config,source,context).toString().getBytes(StandardCharsets.UTF_8);connection.setFixedLengthStreamingMode(body.length);
   try(OutputStream out=connection.getOutputStream()){out.write(body);}
   int code=connection.getResponseCode();
   if(code!=200)throw failure(code);
   ByteArrayOutputStream response=new ByteArrayOutputStream();
   try(InputStream in=connection.getInputStream()){
    byte[] bytes=new byte[4096];int count;while((count=in.read(bytes))!=-1){if(response.size()+count>65536)throw new Failure("رد المحرك أكبر من المتوقع.");response.write(bytes,0,count);}
   }
   return AiRequest.parse(config.provider,response.toString(StandardCharsets.UTF_8.name()));
  }catch(Failure e){throw e;}catch(SocketTimeoutException e){throw new Failure("انتهت مهلة AI. جرّب اتصالًا أفضل أو المحرك المحلي.");}
   catch(Exception e){throw new Failure("تعذر الاتصال بالمحرك أو لم يرجع ترجمة عربية كاملة. راجع النموذج والإعدادات.");}
  finally{if(connection!=null)connection.disconnect();}
 }
 static Failure failure(int code){
  if(code==401||code==403)return new Failure("المفتاح أو صلاحية الخدمة غير مقبولين؛ راجع إعدادات AI.");
  if(code==429||code==402||code==456)return new Failure("بلغت حد الاستخدام أو الرصيد. راجع حساب الخدمة؛ أوقفت المحاولات لمدة 15 ثانية.");
  if(code==400||code==404)return new Failure("النموذج أو صيغة الطلب غير مقبولين. راجع اسم النموذج في إعدادات AI.");
  return new Failure("خدمة AI غير متاحة الآن. جرّب لاحقًا أو اختر المحلي.");
 }
}
