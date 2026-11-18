// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames.phone;
import android.content.Context;
import android.net.wifi.WifiManager;
import dev.arabicgames.Pairing;
import dev.arabicgames.phone.ps5.NativeBridge;
import java.net.*;
import java.util.*;

final class Ps5Discovery {
 static final class Console{final String host,name;final boolean awake;Console(String h,String n,boolean a){host=h;name=n;awake=a;}public String toString(){return name+(awake?" · يعمل":" · وضع الراحة");}}
 static String[] targets()throws Exception{
  LinkedHashSet<String> addresses=new LinkedHashSet<>();int networks=0;
  for(NetworkInterface nic:Collections.list(NetworkInterface.getNetworkInterfaces())){
   if(!nic.isUp()||nic.isLoopback()||nic.getName().startsWith("tun"))continue;
   for(InterfaceAddress address:nic.getInterfaceAddresses()){
    String ip=address.getAddress().getHostAddress();if(!(address.getAddress() instanceof Inet4Address)||!Pairing.privateIpv4(ip))continue;
    if(networks++>=2)break;if(address.getBroadcast()!=null&&Pairing.privateIpv4(address.getBroadcast().getHostAddress()))addresses.add(address.getBroadcast().getHostAddress());
    if(address.getNetworkPrefixLength()>=24&&address.getNetworkPrefixLength()<32){String prefix=ip.substring(0,ip.lastIndexOf('.')+1);for(int n=1;n<255;n++)addresses.add(prefix+n);}
   }
  }return addresses.toArray(new String[0]);
 }
 static List<Console> scan(Context context)throws Exception{
  if(!NativeBridge.available())throw new IllegalStateException("Native engine unavailable");
  Map<String,Console> found=new LinkedHashMap<>();WifiManager.MulticastLock lock=null;
  try{WifiManager wifi=(WifiManager)context.getApplicationContext().getSystemService(Context.WIFI_SERVICE);if(wifi!=null){lock=wifi.createMulticastLock("ArabicGames:PS5-discovery");lock.acquire();}
   NativeBridge.INSTANCE.discover(targets(),3000,(host,name,awake)->{if(!Pairing.privateIpv4(host))return;String label=new String(name,java.nio.charset.StandardCharsets.UTF_8);synchronized(found){if(found.size()<16||found.containsKey(host))found.put(host,new Console(host,label.isBlank()?"PS5":label.substring(0,Math.min(100,label.length())),awake));}});
  }finally{if(lock!=null&&lock.isHeld())lock.release();}
  synchronized(found){return new ArrayList<>(found.values());}
 }
 static Boolean awake(String host){final Boolean[] result={null};NativeBridge.INSTANCE.discover(new String[]{host},1500,(ip,n,on)->{if(host.equals(ip))result[0]=on;});return result[0];}
}
