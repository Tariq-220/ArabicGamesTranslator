// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames.phone;
import org.junit.Test;
import static org.junit.Assert.*;
import java.io.*;
import java.net.*;
import java.nio.charset.StandardCharsets;
public class RelayServerTest {
 @Test public void authenticatesAndStreamsArabicWithoutBufferingOldLines()throws Exception {
  RelayServer relay=new RelayServer("01234567",0);relay.start(2000,false);
  int port=relay.getListeningPort();
  try{
   HttpURLConnection denied=(HttpURLConnection)new URL("http://127.0.0.1:"+port+"/events?code=wrong").openConnection();denied.setReadTimeout(2000);assertEquals(403,denied.getResponseCode());denied.disconnect();
   HttpURLConnection ok=(HttpURLConnection)new URL("http://127.0.0.1:"+port+"/events?code=01234567").openConnection();ok.setReadTimeout(2000);assertEquals(200,ok.getResponseCode());assertEquals(1,relay.clients());
   relay.subtitle("آرثر: لدينا 25 دولارًا.",4500);
   try(BufferedReader input=new BufferedReader(new InputStreamReader(ok.getInputStream(),StandardCharsets.UTF_8))){
    assertEquals("event: subtitle",input.readLine());String json=input.readLine();assertTrue(json.contains("آرثر: لدينا 25 دولارًا."));assertTrue(json.contains("4500"));assertEquals("",input.readLine());
    relay.clear();assertEquals("event: clear",input.readLine());
   }finally{ok.disconnect();}
  }finally{relay.stop();}
  assertEquals(0,relay.clients());
 }

 @Test public void replacesOldConnectionForSameReceiverWithoutExhaustingSlots()throws Exception{
  RelayServer relay=new RelayServer("01234567",0);relay.start(2000,false);
  java.util.List<HttpURLConnection> connections=new java.util.ArrayList<>();
  try{
   for(int i=0;i<12;i++){
    HttpURLConnection c=(HttpURLConnection)new URL("http://127.0.0.1:"+relay.getListeningPort()+"/events?code=01234567&receiver=receiver-one").openConnection();
    c.setReadTimeout(3000);connections.add(c);assertEquals(200,c.getResponseCode());assertEquals(1,relay.clients());
   }
  }finally{for(HttpURLConnection c:connections)c.disconnect();relay.stop();}
  assertEquals(0,relay.clients());
 }
 @Test public void sendsHeartbeatsWhenNoSubtitleAndContinuesAfterLongIdle()throws Exception{
  RelayServer relay=new RelayServer("01234567",0);relay.start(2000,false);
  HttpURLConnection c=(HttpURLConnection)new URL("http://127.0.0.1:"+relay.getListeningPort()+"/events?code=01234567&receiver=idle-receiver").openConnection();c.setReadTimeout(5000);
  try(BufferedReader lines=new BufferedReader(new InputStreamReader(c.getInputStream(),StandardCharsets.UTF_8))){
   assertEquals(": heartbeat",lines.readLine());assertEquals("",lines.readLine());
   Thread.sleep(16000);relay.subtitle("الاتصال مستمر",4500);
   String line;boolean found=false;for(int i=0;i<30&&(line=lines.readLine())!=null;i++)if(line.contains("الاتصال مستمر")){found=true;break;}
   assertTrue(found);assertEquals(1,relay.clients());
  }finally{c.disconnect();relay.stop();}
 }

 @Test public void lateReceiverGetsCurrentTranslationAndClearPreventsReplay()throws Exception{
  RelayServer relay=new RelayServer("01234567",0,50);relay.start(2000,false);relay.subtitle("وصلت الترجمة",5000);
  HttpURLConnection first=(HttpURLConnection)new URL("http://127.0.0.1:"+relay.getListeningPort()+"/events?code=01234567&receiver=late-client-one").openConnection();first.setReadTimeout(2000);
  try(BufferedReader lines=new BufferedReader(new InputStreamReader(first.getInputStream(),StandardCharsets.UTF_8))){assertEquals("event: subtitle",lines.readLine());assertTrue(lines.readLine().contains("وصلت الترجمة"));}
  finally{first.disconnect();}
  relay.clear();HttpURLConnection second=(HttpURLConnection)new URL("http://127.0.0.1:"+relay.getListeningPort()+"/events?code=01234567&receiver=late-client-two").openConnection();second.setReadTimeout(2000);
  try(BufferedReader lines=new BufferedReader(new InputStreamReader(second.getInputStream(),StandardCharsets.UTF_8))){assertEquals(": heartbeat",lines.readLine());}
  finally{second.disconnect();relay.stop();}
 }
}
