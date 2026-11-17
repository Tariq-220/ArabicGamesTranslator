// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames.phone;
import fi.iki.elonen.NanoHTTPD;
import java.util.*;
import java.util.concurrent.atomic.AtomicReference;
import org.json.*;
import org.junit.*;
import static org.junit.Assert.*;

public class AiClientTest {
 @Test public void fasterGeminiKeepsOnlyLatestContextAndMinimumThinkingOnSupportedModel()throws Exception{
  AiPreferences.Config config=new AiPreferences.Config("gemini","gemini-3.5-flash-lite","RDR2","key",true,true);
  JSONObject body=AiRequest.body(config,"Current line.",List.of("oldest","middle","latest"));
  JSONObject input=new JSONObject(body.getJSONArray("contents").getJSONObject(0).getJSONArray("parts").getJSONObject(0).getString("text"));
  assertEquals(1,input.getJSONArray("previous_dialogue_context_only").length());assertEquals("latest",input.getJSONArray("previous_dialogue_context_only").getString(0));
  assertEquals("minimal",body.getJSONObject("generationConfig").getJSONObject("thinkingConfig").getString("thinkingLevel"));
  assertEquals(512,body.getJSONObject("generationConfig").getInt("maxOutputTokens"));
 }
 private static AiPreferences.Config config(String provider){return new AiPreferences.Config(provider,AiPreferences.defaultModel(provider),"Red Dead Redemption 2","test-key-never-log",true);}
 @Test public void openAiHttpRequestCarriesOnlyDialogueAndKeyInHeader()throws Exception{
  AtomicReference<String> auth=new AtomicReference<>(),body=new AtomicReference<>(),uri=new AtomicReference<>();
  NanoHTTPD server=new NanoHTTPD("127.0.0.1",0){public Response serve(IHTTPSession session){
   try{Map<String,String> files=new HashMap<>();session.parseBody(files);body.set(files.get("postData"));auth.set(session.getHeaders().get("authorization"));uri.set(session.getUri());}
   catch(Exception e){throw new AssertionError(e);}
   return newFixedLengthResponse(Response.Status.OK,"application/json","{\"choices\":[{\"finish_reason\":\"stop\",\"message\":{\"content\":\"استيقظي!\"}}]}");
  }};server.start();
  try{
   assertEquals("استيقظي!",AiClient.send(config("openai"),"Rouse yourself.",List.of("You big dozy lizard."),"http://127.0.0.1:"+server.getListeningPort()+"/chat"));
   assertEquals("Bearer test-key-never-log",auth.get());assertEquals("/chat",uri.get());assertFalse(body.get().contains("test-key-never-log"));
   JSONObject request=new JSONObject(body.get());assertEquals("none",request.getString("reasoning_effort"));
   JSONObject input=new JSONObject(request.getJSONArray("messages").getJSONObject(1).getString("content"));assertEquals("Rouse yourself.",input.getString("current_subtitle"));assertEquals(1,input.getJSONArray("previous_dialogue_context_only").length());assertFalse(body.get().contains("image_url"));
  }finally{server.stop();}
 }
 @Test public void providerErrorsDoNotExposeResponseOrKey()throws Exception{
  NanoHTTPD server=new NanoHTTPD("127.0.0.1",0){public Response serve(IHTTPSession session){return newFixedLengthResponse(Response.Status.UNAUTHORIZED,"application/json","private response test-key-never-log");}};server.start();
  try{AiClient.send(config("gemini"),"Keep going.",List.of(),"http://127.0.0.1:"+server.getListeningPort()+"/");fail();}
  catch(AiClient.Failure expected){assertFalse(expected.getMessage().contains("private"));assertFalse(expected.getMessage().contains("test-key"));}
  finally{server.stop();}
 }
 @Test public void noConsentNeverCallsProvider(){
  try{AiClient.translate(new AiPreferences.Config("openai","model","game","key",false),"Hello.",List.of());fail();}
  catch(AiClient.Failure expected){assertTrue(expected.getMessage().contains("وافق"));}
 }
 @Test public void geminiOmitsThoughtsAndRejectsTruncatedOutput()throws Exception{
  assertEquals("واصل التقدم.",AiRequest.parse("gemini","{\"candidates\":[{\"finishReason\":\"STOP\",\"content\":{\"parts\":[{\"thought\":true,\"text\":\"internal\"},{\"text\":\"واصل التقدم.\"}]}}]}"));
  try{AiRequest.parse("gemini","{\"candidates\":[{\"finishReason\":\"MAX_TOKENS\",\"content\":{\"parts\":[{\"text\":\"نص ناقص\"}]}}]}");fail();}catch(Exception expected){}
 }
 @Test public void geminiUsesProviderNativeBodyWithoutOpenAiFields()throws Exception{
  JSONObject body=AiRequest.body(config("gemini"),"Through the palms.",List.of());
  assertTrue(body.has("systemInstruction"));assertTrue(body.has("contents"));assertFalse(body.has("messages"));assertFalse(body.toString().contains("test-key"));
 }
}
