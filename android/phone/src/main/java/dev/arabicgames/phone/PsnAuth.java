// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames.phone;
import java.net.*;
import java.io.*;
import java.nio.charset.StandardCharsets;
import org.json.*;
final class PsnAuth {
 static final class Failure extends Exception{final int status;Failure(int s){super("Sony HTTP "+s);status=s;}}
 private static JSONObject request(String method,String address,String body)throws Exception{
  HttpURLConnection c=(HttpURLConnection)new URL(address).openConnection();c.setInstanceFollowRedirects(false);c.setConnectTimeout(15000);c.setReadTimeout(20000);c.setRequestMethod(method);
  c.setRequestProperty("Authorization","Basic "+java.util.Base64.getEncoder().encodeToString((PsnProtocol.CLIENT+":"+PsnProtocol.PUBLIC_CLIENT_SECRET).getBytes(StandardCharsets.UTF_8)));
  try{if(body!=null){c.setDoOutput(true);c.setRequestProperty("Content-Type","application/x-www-form-urlencoded");try(OutputStream out=c.getOutputStream()){out.write(body.getBytes(StandardCharsets.UTF_8));}}
   int status=c.getResponseCode();if(status!=200)throw new Failure(status);ByteArrayOutputStream data=new ByteArrayOutputStream();try(InputStream in=c.getInputStream()){byte[] b=new byte[4096];int n;while((n=in.read(b))!=-1){if(data.size()+n>128000)throw new IOException();data.write(b,0,n);}}
   return new JSONObject(data.toString("UTF-8"));
  }finally{c.disconnect();}
 }
 static byte[] account(String code)throws Exception{
  JSONObject token=request("POST",PsnProtocol.TOKEN,"grant_type=authorization_code&code="+PsnProtocol.enc(code)+"&scope="+PsnProtocol.enc(PsnProtocol.SCOPE)+"&redirect_uri="+PsnProtocol.enc(PsnProtocol.REDIRECT));String access=token.optString("access_token");if(access.isBlank()||access.length()>8192)throw new IOException();
  JSONObject info=request("GET",PsnProtocol.TOKEN+"/"+PsnProtocol.enc(access),null);return PsnProtocol.accountId(info.optString("user_id"));
 }
}
