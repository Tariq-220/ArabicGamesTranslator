// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames;
import org.junit.Test;
import static org.junit.Assert.*;
public class LanEndpointTest {
 @Test public void SupportsSavedIpAndExplicitFallbackPort(){assertEquals("192.168.1.3:8787",LanEndpoint.authority("192.168.1.3"));assertEquals("10.1.2.3:49152",LanEndpoint.authority("http://10.1.2.3:49152"));}
 @Test public void RejectsNonLanOrInjectedEndpoint(){for(String input:new String[]{"https://10.1.1.1","10.1.1.1:0","10.1.1.1:65536","8.8.8.8:8787","10.1.1.1@evil.example","10.1.1.1/x","10.1.1.1:8787?x=1"})assertFalse(input,LanEndpoint.valid(input));}
}
