// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames.tv;
import java.net.*;
import java.io.*;
import java.nio.charset.StandardCharsets;
import java.util.*;
import java.util.concurrent.*;

/** Bounded, explicit LAN discovery; only probes the current private IPv4 subnet. */
public final class DesktopDiscovery {
 private DesktopDiscovery(){}
 static String subnet(String ip){
  try{String[] p=ip.split("\\.");if(p.length!=4)return null;int[] n=new int[4];for(int i=0;i<4;i++){n[i]=Integer.parseInt(p[i]);if(n[i]<0||n[i]>255)return null;}
   if(!(n[0]==10||n[0]==192&&n[1]==168||n[0]==172&&n[1]>=16&&n[1]<=31))return null;
   return n[0]+"."+n[1]+"."+n[2];
  }catch(Exception e){return null;}
 }
 public static List<String> find() throws Exception {
  Set<String> subnets=new LinkedHashSet<>();
  for(NetworkInterface nic:Collections.list(NetworkInterface.getNetworkInterfaces())){
   if(!nic.isUp()||nic.isLoopback())continue;
   for(InetAddress ip:Collections.list(nic.getInetAddresses()))if(ip instanceof Inet4Address){String sub=subnet(ip.getHostAddress());if(sub!=null)subnets.add(sub);}
  }
  Set<String> found=Collections.synchronizedSet(new TreeSet<>());ExecutorService pool=Executors.newFixedThreadPool(16);
  try{
   int scanned=0;
   for(String sub:subnets){if(scanned++>=2)break;for(int i=1;i<255;i++){String host=sub+"."+i;pool.submit(()->{if(isDesktop(host))found.add(host);});}}
   pool.shutdown();if(!pool.awaitTermination(25,TimeUnit.SECONDS))pool.shutdownNow();
  }finally{pool.shutdownNow();}
  synchronized(found){return new ArrayList<>(found);}
 }
 private static boolean isDesktop(String host){HttpURLConnection c=null;
  try{
   c=(HttpURLConnection)new URL("http://"+host+":8787/hello").openConnection();c.setConnectTimeout(450);c.setReadTimeout(450);c.setInstanceFollowRedirects(false);
   if(c.getResponseCode()!=200)return false;
   ByteArrayOutputStream b=new ByteArrayOutputStream();try(InputStream in=c.getInputStream()){byte[] buf=new byte[256];int n;while((n=in.read(buf))!=-1){b.write(buf,0,n);if(b.size()>2048)return false;}}
   String body=b.toString(StandardCharsets.UTF_8.name());return body.contains("\"gametranslator\"")&&body.contains("\"app\"");
  }catch(Exception e){return false;}finally{if(c!=null)c.disconnect();}
 }
}
