// SPDX-License-Identifier: AGPL-3.0-only
// Sony public Remote Play client parameters from chiaki-ng / Sefflex psn.dart.
package dev.arabicgames.phone;
import java.net.*;
import java.nio.charset.StandardCharsets;
import java.security.*;
import java.math.BigInteger;
import java.util.*;
final class PsnProtocol {
 static final String CLIENT="ba495a24-818c-472b-b12d-ff231c1b5745",PUBLIC_CLIENT_SECRET="mvaiZkRsAsI1IBkY";
 static final String REDIRECT="https://remoteplay.dl.playstation.net/remoteplay/redirect",TOKEN="https://auth.api.sonyentertainmentnetwork.com/2.0/oauth/token";
 static final String SCOPE="psn:clientapp referenceDataService:countryConfig.read pushNotification:webSocket.desktop.connect sessionManager:remotePlaySession.system.update";
 static String nonce(){byte[] b=new byte[24];new SecureRandom().nextBytes(b);StringBuilder s=new StringBuilder();for(byte v:b)s.append(String.format(java.util.Locale.ROOT,"%02x",v&255));return s.toString();}
 static String enc(String s){try{return URLEncoder.encode(s,"UTF-8").replace("+","%20");}catch(java.io.UnsupportedEncodingException e){throw new AssertionError(e);}}
 static String dec(String s){try{return URLDecoder.decode(s,"UTF-8");}catch(java.io.UnsupportedEncodingException e){throw new AssertionError(e);}}
 static String authorize(String state){return "https://auth.api.sonyentertainmentnetwork.com/2.0/oauth/authorize?service_entity=urn%3Aservice-entity%3Apsn&response_type=code&client_id="+CLIENT+"&redirect_uri="+enc(REDIRECT)+"&scope="+enc(SCOPE)+"&request_locale=en_US&ui=pr&service_logo=ps&layout_type=popup&smcid=remoteplay&prompt=always&PlatformPrivacyWs1=minimal&state="+enc(state);}
 static boolean redirect(String value){try{URI u=new URI(value);return "https".equals(u.getScheme())&&"remoteplay.dl.playstation.net".equals(u.getHost())&&"/remoteplay/redirect".equals(u.getPath())&&(u.getPort()==-1||u.getPort()==443)&&u.getUserInfo()==null;}catch(Exception e){return false;}}
 static String code(String value,String state){if(!redirect(value))throw new IllegalArgumentException();try{
  URI u=new URI(value);Map<String,String> q=new HashMap<>();if(u.getRawQuery()==null)throw new IllegalArgumentException();for(String pair:u.getRawQuery().split("&")){String[] kv=pair.split("=",2);String k=dec(kv[0]),v=kv.length==2?dec(kv[1]):"";if(q.put(k,v)!=null)throw new IllegalArgumentException();}
  String actual=q.getOrDefault("state","");if(state==null||state.length()<32||!MessageDigest.isEqual(state.getBytes(StandardCharsets.UTF_8),actual.getBytes(StandardCharsets.UTF_8))||q.containsKey("error"))throw new IllegalArgumentException();String code=q.get("code");if(code==null||code.isBlank()||code.length()>4096)throw new IllegalArgumentException();return code;
 }catch(URISyntaxException e){throw new IllegalArgumentException();}}
 static byte[] accountId(String decimal){if(decimal==null||!decimal.matches("[0-9]{1,20}"))throw new IllegalArgumentException();BigInteger n=new BigInteger(decimal);if(n.bitLength()>64)throw new IllegalArgumentException();byte[] out=new byte[8];for(int i=0;i<8;i++){out[i]=n.byteValue();n=n.shiftRight(8);}return out;}
 static boolean sonyPage(String value){try{URI u=new URI(value);String host=u.getHost();return "https".equals(u.getScheme())&&host!=null&&u.getUserInfo()==null&&(u.getPort()==-1||u.getPort()==443)&&java.util.stream.Stream.of("sonyentertainmentnetwork.com","sony.com","playstation.com").anyMatch(d->host.equals(d)||host.endsWith("."+d));}catch(Exception e){return false;}}
}
