// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames.phone;
import org.junit.Test;
import org.json.JSONObject;
import static org.junit.Assert.*;
import java.net.*;
import java.nio.charset.StandardCharsets;
public class LatestCaptionTest {
 private JSONObject latest(RelayServer server)throws Exception{HttpURLConnection c=(HttpURLConnection)new URL("http://127.0.0.1:"+server.getListeningPort()+"/latest?code=01234567&receiver=tv-poll-client").openConnection();c.setReadTimeout(2000);try{assertEquals(200,c.getResponseCode());return new JSONObject(new String(c.getInputStream().readAllBytes(),StandardCharsets.UTF_8));}finally{c.disconnect();}}
 @Test public void pollingGetsArabicAndDoesNotDuplicateScreenCount()throws Exception{RelayServer s=new RelayServer("01234567",0);s.start(2000,false);try{s.subtitle("ترجمة اللعبة الحالية",5000);JSONObject one=latest(s);assertTrue(one.getBoolean("active"));assertEquals("ترجمة اللعبة الحالية",one.getString("text"));assertTrue(one.getInt("duration")>0);long revision=one.getLong("revision");latest(s);assertEquals(1,s.clients());s.clear();JSONObject cleared=latest(s);assertFalse(cleared.getBoolean("active"));assertTrue(cleared.getLong("revision")>revision);}finally{s.stop();}}
 @Test public void latestRequiresThePairingCode()throws Exception{RelayServer s=new RelayServer("01234567",0);s.start(2000,false);try{HttpURLConnection c=(HttpURLConnection)new URL("http://127.0.0.1:"+s.getListeningPort()+"/latest?code=wrong").openConnection();assertEquals(403,c.getResponseCode());c.disconnect();}finally{s.stop();}}
}
