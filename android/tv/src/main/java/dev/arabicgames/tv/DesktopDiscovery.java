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
 public static List<String> find() throws Exception {return find(null);}
 static java.util.List<String> priorityHosts(java.util.Collection<String> gateways,java.util.Collection<String> subnets){LinkedHashSet<String> out=new LinkedHashSet<>();for(String ip:gateways)if(subnet(ip)!=null&&out.size()<8)out.add(ip);for(String sub:subnets)if(subnet(sub+".1")!=null&&out.size()<8)out.add(sub+".1");return new ArrayList<>(out);}
 public static List<String> find(android.content.Context context) throws Exception {return find(context,false);}
 public static List<String> find(android.content.Context context,boolean fullSearch) throws Exception {
  Set<String> subnets=new LinkedHashSet<>();
  for(NetworkInterface nic:Collections.list(NetworkInterface.getNetworkInterfaces())){
   if(!nic.isUp()||nic.isLoopback())continue;
   for(InetAddress ip:Collections.list(nic.getInetAddresses()))if(ip instanceof Inet4Address){String sub=subnet(ip.getHostAddress());if(sub!=null)subnets.add(sub);}
  }
  List<String> gateways=new ArrayList<>();
  try{if(context!=null){android.net.ConnectivityManager cm=(android.net.ConnectivityManager)context.getSystemService(android.content.Context.CONNECTIVITY_SERVICE);if(cm!=null)for(android.net.Network network:cm.getAllNetworks()){android.net.LinkProperties links=cm.getLinkProperties(network);if(links!=null)for(android.net.RouteInfo route:links.getRoutes()){InetAddress gateway=route.getGateway();if(gateway instanceof Inet4Address&&subnet(gateway.getHostAddress())!=null)gateways.add(gateway.getHostAddress());}}}}catch(Exception ignored){}
  List<String> priority=priorityHosts(gateways,subnets);Set<String> found=Collections.synchronizedSet(new TreeSet<>());ExecutorService fast=Executors.newFixedThreadPool(4);
  try{for(String host:priority)fast.submit(()->{String type=probe(host);if(type!=null)found.add(type+host);});fast.shutdown();if(!fast.awaitTermination(3,TimeUnit.SECONDS))fast.shutdownNow();}finally{fast.shutdownNow();}
  if(!fullSearch&&!found.isEmpty()){synchronized(found){return new ArrayList<>(found);}}
  ExecutorService pool=Executors.newFixedThreadPool(16);
  try{
   int scanned=0;
   for(String sub:subnets){if(scanned++>=2)break;for(int i=1;i<255;i++){String host=sub+"."+i;pool.submit(()->{String type=probe(host);if(type!=null)found.add(type+host);});}}
   pool.shutdown();if(!pool.awaitTermination(25,TimeUnit.SECONDS))pool.shutdownNow();
  }finally{pool.shutdownNow();}
  synchronized(found){return new ArrayList<>(found);}
 }
 private static String probe(String host){HttpURLConnection c=null;
  try{
   c=(HttpURLConnection)new URL("http://"+host+":8787/hello").openConnection();c.setConnectTimeout(450);c.setReadTimeout(450);c.setInstanceFollowRedirects(false);
   if(c.getResponseCode()!=200)return null;
   ByteArrayOutputStream b=new ByteArrayOutputStream();try(InputStream in=c.getInputStream()){byte[] buf=new byte[256];int n;while((n=in.read(buf))!=-1){b.write(buf,0,n);if(b.size()>2048)return null;}}
   String body=b.toString(StandardCharsets.UTF_8.name());if(body.contains("\"arabicgames-phone\""))return "جوال · ";
   return body.contains("\"gametranslator\"")&&body.contains("\"app\"")?"كمبيوتر · ":null;
  }catch(Exception e){return null;}finally{if(c!=null)c.disconnect();}
 }
}
