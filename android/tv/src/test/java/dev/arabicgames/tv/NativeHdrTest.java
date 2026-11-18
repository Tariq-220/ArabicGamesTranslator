package dev.arabicgames.tv;
import android.content.*;import org.junit.*;import org.junit.runner.RunWith;import org.robolectric.*;import org.robolectric.annotation.Config;import static org.junit.Assert.*;
@RunWith(RobolectricTestRunner.class) @Config(sdk=35)
public class NativeHdrTest {
 @Test public void rejectsExternalSourcesAndRequiresPhonePairCode(){assertFalse(NativeHdmiOverlay.validSource("8.8.8.8","",true));assertFalse(NativeHdmiOverlay.validSource("192.168.1.3","",false));assertTrue(NativeHdmiOverlay.validSource("192.168.1.3","",true));}
 @Test public void overlayServiceIsPrivateAndDeclared(){var c=RuntimeEnvironment.getApplication();var r=c.getPackageManager().resolveService(new Intent(c,NativeHdmiOverlay.class),0);assertNotNull(r);assertFalse(r.serviceInfo.exported);}
}
