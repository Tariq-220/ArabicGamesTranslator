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
  Set<String> found=Collections.synchronizedSet(new TreeSet<>());
  findUdp(found);if(!found.isEmpty())return new ArrayList<>(found);ExecutorService pool=Executors.newFixedThreadPool(16);
  try{
   int scanned=0;
   for(String sub:subnets){if(scanned++>=2)break;for(int i=1;i<255;i++){String host=sub+"."+i;pool.submit(()->{String type=probe(host);if(type!=null)found.add(type+host);});}}
   pool.shutdown();if(!pool.awaitTermination(25,TimeUnit.SECONDS))pool.shutdownNow();
  }finally{pool.shutdownNow();}
  synchronized(found){return new ArrayList<>(found);}
 }
 private static void findUdp(Set<String> found){
  String nonce=UUID.randomUUID().toString().replace("-","");byte[] query=("ARABICGAMES_DISCOVER_V1 "+nonce).getBytes(StandardCharsets.US_ASCII);
  try(DatagramSocket socket=new DatagramSocket()){
   socket.setBroadcast(true);socket.setSoTimeout(350);
   for(NetworkInterface nic:Collections.list(NetworkInterface.getNetworkInterfaces()))if(nic.isUp()&&!nic.isLoopback())for(InterfaceAddress address:nic.getInterfaceAddresses()){
    InetAddress broadcast=address.getBroadcast();if(broadcast!=null&&subnet(address.getAddress().getHostAddress())!=null)socket.send(new DatagramPacket(query,query.length,broadcast,8788));
   }
   long until=System.currentTimeMillis()+1200;while(System.currentTimeMillis()<until){try{byte[] bytes=new byte[160];DatagramPacket reply=new DatagramPacket(bytes,bytes.length);socket.receive(reply);String endpoint=discovered(reply.getAddress().getHostAddress(),new String(reply.getData(),0,reply.getLength(),StandardCharsets.US_ASCII),nonce);if(endpoint!=null)found.add("كمبيوتر · "+endpoint);}catch(SocketTimeoutException wait){}}
  }catch(Exception ignored){}
 }
 static String discovered(String source,String body,String nonce){
  if(subnet(source)==null||nonce==null||!nonce.matches("[a-f0-9]{32}"))return null;
  String[] parts=body.split(" ",-1);if(parts.length!=3||!parts[0].equals("ARABICGAMES_V1")||!parts[1].equals(nonce))return null;
  String endpoint=source+":"+parts[2];return dev.arabicgames.LanEndpoint.authority(endpoint);
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
