// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames.tv;
import java.net.*;import java.util.*;
/** Exercise the actual private-endpoint policy rather than bypassing it with loopback. */
final class TestLanAddress {
 static String endpoint(int port)throws Exception{
  for(NetworkInterface nic:Collections.list(NetworkInterface.getNetworkInterfaces()))if(nic.isUp()&&!nic.isLoopback())
   for(InetAddress ip:Collections.list(nic.getInetAddresses()))if(ip instanceof Inet4Address&&dev.arabicgames.Pairing.privateIpv4(ip.getHostAddress()))return ip.getHostAddress()+":"+port;
  throw new AssertionError("LAN transport tests need a private IPv4 interface");
 }
}
