package dev.arabicgames.phone;
import org.junit.Test;
import static org.junit.Assert.*;
import java.io.*;
import java.net.*;
import java.nio.charset.StandardCharsets;
public class RelayControlTest {
 @Test public void pairedStreamReceivesPanelCommandWithoutReplayingAnOldSubtitle()throws Exception{
  RelayServer relay=new RelayServer("01234567",0,100);relay.start(2000,false);
  HttpURLConnection c=(HttpURLConnection)new URL("http://127.0.0.1:"+relay.getListeningPort()+"/events?code=01234567&receiver=settings-test").openConnection();c.setReadTimeout(2000);
  try{assertEquals(200,c.getResponseCode());relay.openTvPanel();try(BufferedReader in=new BufferedReader(new InputStreamReader(c.getInputStream(),StandardCharsets.UTF_8))){assertEquals("event: settings",in.readLine());assertEquals("data: {}",in.readLine());}}
  finally{c.disconnect();relay.stop();}
 }
}
