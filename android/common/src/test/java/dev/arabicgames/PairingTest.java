// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames;
import org.junit.Test;
import static org.junit.Assert.*;
public class PairingTest {
 @Test public void acceptsOnlyPrivateIpv4(){for(String s:new String[]{"10.0.0.1","192.168.1.30","172.16.0.1","172.31.255.254"})assertTrue(s,Pairing.privateIpv4(s));for(String s:new String[]{"8.8.8.8","127.0.0.1","192.168.1.999","172.32.1.2","localhost","192.168.1.1/path","192.168.1.1:8787","1.2.3","10..0.1"})assertFalse(s,Pairing.privateIpv4(s));}
 @Test public void generatedCodesAreEightAsciiDigits(){for(int i=0;i<100;i++)assertTrue(Pairing.validCode(Pairing.createCode()));}
}
