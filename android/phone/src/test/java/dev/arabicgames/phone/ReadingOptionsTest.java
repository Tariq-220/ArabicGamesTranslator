package dev.arabicgames.phone;
import org.junit.*;import org.junit.runner.RunWith;import org.robolectric.*;import org.robolectric.annotation.Config;import static org.junit.Assert.*;
@RunWith(RobolectricTestRunner.class) @Config(sdk=35)
public class ReadingOptionsTest {
 @Test public void phonePresetsIncludeUpperPhoneAndRemainInsideVideo(){for(String mode:new String[]{"phoneRight","phoneLeft"}){float[] r=ReadingRegion.bounds(mode);assertTrue(r[1]<.2);assertTrue(r[0]>=0&&r[1]>=0&&r[0]+r[2]<=1&&r[1]+r[3]<=1);}}
 @Test public void phoneModeDisablesDialogueFilterAndReturningRestoresIt(){var c=RuntimeEnvironment.getApplication();ReadingOptions.select(c,4);assertEquals("phoneRight",c.getSharedPreferences("reading",0).getString("region",""));assertFalse(c.getSharedPreferences("reading",0).getBoolean("dialogueOnly",true));ReadingOptions.select(c,0);assertTrue(c.getSharedPreferences("reading",0).getBoolean("dialogueOnly",false));}
}
