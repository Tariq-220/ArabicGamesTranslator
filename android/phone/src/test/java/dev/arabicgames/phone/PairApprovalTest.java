package dev.arabicgames.phone;
import org.junit.Test;
import static org.junit.Assert.*;
import fi.iki.elonen.NanoHTTPD;
import java.util.*;
import java.lang.reflect.Proxy;
public class PairApprovalTest {
 private NanoHTTPD.Response call(RelayServer relay,String ip,String path,String ticket,boolean browser){
  NanoHTTPD.IHTTPSession session=(NanoHTTPD.IHTTPSession)Proxy.newProxyInstance(getClass().getClassLoader(),new Class[]{NanoHTTPD.IHTTPSession.class},(p,m,a)->switch(m.getName()){
   case "getMethod" -> NanoHTTPD.Method.GET;
   case "getRemoteIpAddress" -> ip;
   case "getUri" -> path;
   case "getParms" -> ticket==null?Map.of():Map.of("ticket",ticket);
   case "getHeaders" -> browser?Map.of("origin","http://evil.example"):Map.of();
   default -> null;
  });return relay.serve(session);
 }
 private String body(NanoHTTPD.Response r)throws Exception{return new String(r.getData().readAllBytes(),java.nio.charset.StandardCharsets.UTF_8);}
 @Test public void secretRequiresMatchingApprovalAndRequester()throws Exception{
  RelayServer relay=new RelayServer("12345678",0);String ticket="abcdef0123456789abcdef0123456789";
  assertFalse(body(call(relay,"192.168.1.8","/hello",null,false)).contains("12345678"));
  assertFalse(body(call(relay,"192.168.1.8","/pair",ticket,false)).contains("12345678"));
  relay.approve("wrong",true);assertFalse(body(call(relay,"192.168.1.8","/pair",ticket,false)).contains("12345678"));
  relay.approve("abcdef",true);assertTrue(body(call(relay,"192.168.1.8","/pair",ticket,false)).contains("12345678"));
  assertEquals(503,call(relay,"192.168.1.9","/pair",ticket,false).getStatus().getRequestStatus());
  relay.stop();
 }
 @Test public void rejectsPublicClientsBrowserAndMalformedTickets(){
  RelayServer relay=new RelayServer("12345678",0);
  assertEquals(403,call(relay,"8.8.8.8","/hello",null,false).getStatus().getRequestStatus());
  assertEquals(403,call(relay,"192.168.1.8","/pair","a".repeat(32),true).getStatus().getRequestStatus());
  assertEquals(400,call(relay,"192.168.1.8","/pair","bad",false).getStatus().getRequestStatus());relay.stop();
 }
}
