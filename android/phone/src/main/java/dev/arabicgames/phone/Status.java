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
 static String address() {
  try { for(NetworkInterface ni:Collections.list(NetworkInterface.getNetworkInterfaces())) {
   if(!ni.isUp()||ni.isLoopback())continue;
   for(InetAddress a:Collections.list(ni.getInetAddresses()))if(a instanceof Inet4Address && a.isSiteLocalAddress())return a.getHostAddress();
  }}catch(Exception ignored){}return "غير متصل بشبكة محلية";
 }
}
