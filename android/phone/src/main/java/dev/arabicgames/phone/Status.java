// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames.phone;
import java.net.*;
import java.util.*;
final class Status {
 static volatile boolean running,captureActive,modelReady,translatorUiVisible;
 static volatile String message="نزّل نموذج العربية ثم ابدأ اختبار ربط التلفزيون.";
 static volatile String code="", source="", arabic="", metrics="";
 static volatile String cloudMetrics="آخر طلب AI: لم يكتمل طلب جديد في هذه الجلسة بعد.";
 static volatile int clients,framesRead;
 static volatile String observedOcr="";
 static int networkRank(String name,String ip){name=name.toLowerCase(Locale.ROOT);if(name.startsWith("ap")||name.contains("swlan")||name.contains("p2p"))return 0;if(name.contains("wlan")||name.contains("wifi"))return ip.endsWith(".1")?1:2;if(name.startsWith("eth")||name.startsWith("rndis"))return 3;if(name.startsWith("rmnet")||name.startsWith("pdp")||name.startsWith("ccmni")||name.startsWith("wwan"))return 20;return 10;}
 static String address() {
  String best=null;int rank=Integer.MAX_VALUE;
  try { for(NetworkInterface ni:Collections.list(NetworkInterface.getNetworkInterfaces())) {
   if(!ni.isUp()||ni.isLoopback())continue;
   for(InetAddress a:Collections.list(ni.getInetAddresses()))if(a instanceof Inet4Address && a.isSiteLocalAddress()){int score=networkRank(ni.getName(),a.getHostAddress());if(score<rank){best=a.getHostAddress();rank=score;}}
  }}catch(Exception ignored){}return best==null?"غير متصل بشبكة محلية":best;
 }
}
