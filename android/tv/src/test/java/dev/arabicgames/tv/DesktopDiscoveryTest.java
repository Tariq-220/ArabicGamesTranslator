package dev.arabicgames.tv;
import org.junit.Test;
import static org.junit.Assert.*;
public class DesktopDiscoveryTest {
 @Test public void hotspotGatewayIsTriedBeforeTheHomeRouter(){var hosts=DesktopDiscovery.priorityHosts(java.util.Arrays.asList("172.20.10.1","8.8.8.8","127.0.0.1"),java.util.Arrays.asList("172.20.10","192.168.43"));assertEquals(java.util.Arrays.asList("172.20.10.1","192.168.43.1"),hosts);}
 @Test public void onlyScansPrivateIpv4Networks(){
  assertEquals("192.168.0",DesktopDiscovery.subnet("192.168.0.118"));
  assertEquals("10.1.2",DesktopDiscovery.subnet("10.1.2.3"));
  assertEquals("172.31.4",DesktopDiscovery.subnet("172.31.4.8"));
  for(String ip:new String[]{"8.8.8.8","127.0.0.1","169.254.1.2","172.32.0.1","192.168.0.256","::1","192.168.1","192.168.1.2/24"})assertNull(ip,DesktopDiscovery.subnet(ip));
 }
}
