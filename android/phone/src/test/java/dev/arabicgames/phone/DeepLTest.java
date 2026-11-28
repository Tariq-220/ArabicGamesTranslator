// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames.phone;
import fi.iki.elonen.NanoHTTPD;
import java.util.*;
import java.util.concurrent.atomic.AtomicReference;
import org.json.*;
import org.junit.Test;
import static org.junit.Assert.*;
public class DeepLTest {
 @Test public void usesOfficialEndpointsForFreeAndProKeys(){
  assertEquals("https://api-free.deepl.com/v2/translate",AiClient.endpoint(new AiPreferences.Config("deepl","auto","game","free:fx",true)));
  assertEquals("https://api.deepl.com/v2/translate",AiClient.endpoint(new AiPreferences.Config("deepl","auto","game","pro",true)));
 }
 @Test public void sendsEnglishToArabicWithContextAndHeaderKey()throws Exception{
  AtomicReference<String> auth=new AtomicReference<>(),payload=new AtomicReference<>();
  NanoHTTPD server=new NanoHTTPD("127.0.0.1",0){public Response serve(IHTTPSession session){
   try{Map<String,String> files=new HashMap<>();session.parseBody(files);payload.set(files.get("postData"));auth.set(session.getHeaders().get("authorization"));}catch(Exception e){throw new AssertionError(e);}
   return newFixedLengthResponse(Response.Status.OK,"application/json","{\"translations\":[{\"text\":\"واصل التقدم.\",\"detected_source_language\":\"EN\"}]}");
  }};server.start();
  try{
   String actual=AiClient.send(new AiPreferences.Config("deepl","auto","RDR2","fake-key:fx",true),"Keep going.",List.of("Through the palms. → عبر النخيل."),"http://127.0.0.1:"+server.getListeningPort()+"/v2/translate");
   assertEquals("واصل التقدم.",actual);assertEquals("DeepL-Auth-Key fake-key:fx",auth.get());
   JSONObject body=new JSONObject(payload.get());assertEquals("EN",body.getString("source_lang"));assertEquals("AR",body.getString("target_lang"));assertEquals("Keep going.",body.getJSONArray("text").getString(0));assertTrue(body.getString("context").startsWith(AiRequest.INSTRUCTION));assertTrue(body.getString("context").endsWith("Through the palms."));
   assertFalse(body.has("messages"));assertFalse(body.has("model_type"));assertFalse(payload.get().contains("fake-key"));
  }finally{server.stop();}
 }
}
