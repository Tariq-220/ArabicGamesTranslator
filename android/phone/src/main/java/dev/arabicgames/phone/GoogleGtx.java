// SPDX-License-Identifier: AGPL-3.0-only
// Endpoint/response strategy derived from Sefflex/GameTranslator v1.7.0 Translators.kt.
package dev.arabicgames.phone;
import java.net.*;
import java.io.*;
import java.nio.charset.StandardCharsets;
import org.json.*;
final class GoogleGtx {
 static String translate(String source)throws AiClient.Failure {
  try {String q=URLEncoder.encode(source.replaceAll("\\s*\\n\\s*"," "),"UTF-8");return request(new String[]{
   "https://translate.googleapis.com/translate_a/single?client=gtx&sl=en&tl=ar&dt=t&q="+q,
   "https://translate.googleapis.com/translate_a/single?client=dict-chrome-ex&sl=en&tl=ar&dt=t&q="+q});
  }catch(AiClient.Failure e){throw e;}catch(Exception e){throw new AiClient.Failure("تعذر تجهيز طلب Google.");}
 }
 static String request(String[] endpoints)throws AiClient.Failure {
  long end=android.os.SystemClock.elapsedRealtime()+4000;
  for(String endpoint:endpoints){HttpURLConnection c=null;
   try{int remaining=(int)(end-android.os.SystemClock.elapsedRealtime());if(remaining<100)break;
    c=(HttpURLConnection)new URL(endpoint).openConnection();c.setInstanceFollowRedirects(false);c.setConnectTimeout(Math.min(2000,remaining));c.setReadTimeout(remaining);c.setRequestProperty("User-Agent","Mozilla/5.0");
    int code=c.getResponseCode();if(code==429)throw new AiClient.Failure("وصل Google إلى حد الاستخدام. انتظر أو اختر محركًا آخر.");if(code!=200)continue;
    ByteArrayOutputStream out=new ByteArrayOutputStream();try(InputStream in=c.getInputStream()){byte[] bytes=new byte[4096];int n;while((n=in.read(bytes))!=-1){if(out.size()+n>65536)throw new IOException();out.write(bytes,0,n);}}
    return parse(out.toString(StandardCharsets.UTF_8.name()));
   }catch(AiClient.Failure e){throw e;}catch(Exception e){}finally{if(c!=null)c.disconnect();}
  }
  throw new AiClient.Failure("تعذر اتصال Google بدون مفتاح. لا يوجد تبديل تلقائي لمحرك آخر.");
 }
 static String parse(String value)throws Exception {
  JSONArray data=new JSONArray(value);StringBuilder out=new StringBuilder();Object first=data.opt(0);
  if(first instanceof String)out.append(first);
  else if(first instanceof JSONArray){JSONArray parts=(JSONArray)first;for(int i=0;i<parts.length();i++){JSONArray part=parts.optJSONArray(i);if(part!=null&&!part.isNull(0))out.append(part.optString(0,""));}}
  if(out.toString().trim().isEmpty())throw new IOException("empty translation");
  return android.text.Html.fromHtml(out.toString(),android.text.Html.FROM_HTML_MODE_LEGACY).toString().trim();
 }
}
