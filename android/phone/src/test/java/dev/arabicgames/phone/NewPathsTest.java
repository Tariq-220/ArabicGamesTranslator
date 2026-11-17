// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames.phone;
import java.io.*;
import java.net.*;
import java.util.*;
import org.json.*;
import org.junit.Test;
import static org.junit.Assert.*;
public class NewPathsTest {
 @Test public void flashDialoguePresetUsesSupportedLowThinking()throws Exception{
  JSONObject body=AiRequest.body(new AiPreferences.Config("gemini","gemini-3.8-flash","RDR2","key",true),"Hello.",List.of("Context"));
  assertEquals("low",body.getJSONObject("generationConfig").getJSONObject("thinkingConfig").getString("thinkingLevel"));
 }
 @Test public void historyHasBoundedRowsAndOnlyExportsReviewedRowsByDefault()throws Exception{
  File file=File.createTempFile("dialogue-test",".json");DialogueStore store=new DialogueStore(file);
  try{store.add("RDR2","subtitles","Hello.","ترجمة أولية","engine");long id=store.snapshot().getJSONObject(0).getLong("id");
   try{store.pack("RDR2",true);fail();}catch(IllegalArgumentException expected){}
   store.revise(id,"مرحبًا.");String pack=store.pack("RDR2",true);assertEquals("مرحبًا.",PackFiles.parse(pack).find("Hello."));assertEquals(0,store.snapshot().getJSONObject(0).optString("key").length());
   for(int i=0;i<230;i++)store.add("RDR2","all","Menu "+i,"القائمة "+i,"model");assertEquals(200,store.snapshot().length());store.clear();assertEquals(0,store.snapshot().length());
  }finally{file.delete();}
 }
 @Test public void viewerIsAuthenticatedAndDoesNotEmbedGameTextAsHtml()throws Exception{
  RelayServer server=new RelayServer("01234567",0);server.start();
  try{String base="http://127.0.0.1:"+server.getListeningPort();HttpURLConnection denied=(HttpURLConnection)new URL(base+"/screen").openConnection();assertEquals(403,denied.getResponseCode());denied.disconnect();
   HttpURLConnection allowed=(HttpURLConnection)new URL(base+"/screen?code=01234567").openConnection();assertEquals(200,allowed.getResponseCode());String html=new String(allowed.getInputStream().readAllBytes(),java.nio.charset.StandardCharsets.UTF_8);assertTrue(html.contains("caption.textContent"));assertFalse(html.contains("innerHTML"));assertEquals("no-referrer",allowed.getHeaderField("Referrer-Policy"));allowed.disconnect();
  }finally{server.stop();}
 }
 @Test public void localEndpointRejectsPublicHostsCredentialsAndRedirectPaths(){
  assertEquals("http://192.168.1.2:8790",LocalEndpoint.validate("http://192.168.1.2:8790/"));
  for(String invalid:List.of("http://8.8.8.8:8790","http://localhost:8790","http://192.168.1.2:8790/admin","http://key@192.168.1.2:8790","http://192.168.1.2:8790?key=secret"))try{LocalEndpoint.validate(invalid);fail(invalid);}catch(IllegalArgumentException expected){}
 }
 @Test public void localProtocolDoesNotSendKeysGameOrContext()throws Exception{
  AiPreferences.Config config=new AiPreferences.Config("lan","quickmt","RDR2","private-token",true,false,"http://192.168.1.2:8790");
  assertEquals("http://192.168.1.2:8790/translate",AiClient.endpoint(config));JSONObject body=AiRequest.body(config,"Hello.",List.of("Old dialogue"));assertEquals("quickmt",body.getString("model"));assertFalse(body.toString().contains("private-token"));assertFalse(body.toString().contains("Old dialogue"));assertEquals("مرحبًا.",AiRequest.parse("lan","{\"translations\":[{\"text\":\"مرحبًا.\"}]}"));
 }
 @Test public void menuModeBoundsAndRequestLimitsStayInsideTheFrame(){
  for(String mode:List.of("all","left","right","subtitles")){float[] rect=ReadingRegion.bounds(mode);assertTrue(rect[0]>=0&&rect[1]>=0&&rect[0]+rect[2]<=1.001&&rect[1]+rect[3]<=1.001);}
  assertTrue(ReadingRegion.sourceLimit("Menu line\n".repeat(300)).length()<=1100);
 }
}
