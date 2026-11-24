// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames;
/** Exact private-LAN endpoint; no credentials, public hosts, redirects, paths or DNS. */
public final class LanEndpoint {
 private LanEndpoint(){}
 public static String authority(String value){
  if(value==null)return null;String text=value.trim();if(text.startsWith("http://"))text=text.substring(7);
  if(text.indexOf('/')>=0||text.indexOf('@')>=0||text.indexOf('?')>=0||text.indexOf('#')>=0)return null;
  String[] parts=text.split(":",-1);if(parts.length<1||parts.length>2||!Pairing.privateIpv4(parts[0]))return null;
  int port=8787;try{if(parts.length==2){if(!parts[1].matches("[0-9]{1,5}"))return null;port=Integer.parseInt(parts[1]);}}catch(NumberFormatException error){return null;}
  return port>=1&&port<=65535?parts[0]+":"+port:null;
 }
 public static boolean valid(String value){return authority(value)!=null;}
 public static String origin(String value){String result=authority(value);if(result==null)throw new IllegalArgumentException("Invalid LAN endpoint");return "http://"+result;}
}
