package dev.arabicgames.tv;
import android.os.Looper;import android.widget.*;import org.json.JSONObject;import org.junit.*;import org.junit.runner.RunWith;import org.robolectric.*;import org.robolectric.annotation.Config;import org.robolectric.util.ReflectionHelpers;import java.time.Duration;import static org.junit.Assert.*;
@RunWith(RobolectricTestRunner.class) @Config(sdk=35)
public class TvInterfaceLaneTest {
 @Test public void MenuExpiresWithoutClearingDialogue() throws Exception {try(var c=Robolectric.buildActivity(MainActivity.class).setup()){
  MainActivity a=c.get();TextView dialogue=ReflectionHelpers.getField(a,"subtitle");dialogue.setText("هل أنت مستعد؟");
  JSONObject packet=new JSONObject("{\"duration\":1200,\"items\":[{\"text\":\"اتبع لوسيا\",\"box\":{\"left\":0.1,\"top\":0.3,\"right\":0.4,\"bottom\":0.4}}]}");
  ReflectionHelpers.callInstanceMethod(a,"showInterface",ReflectionHelpers.ClassParameter.from(JSONObject.class,packet));
  FrameLayout layer=ReflectionHelpers.getField(a,"interfaceLayer");assertEquals(1,layer.getChildCount());
  Shadows.shadowOf(Looper.getMainLooper()).idleFor(Duration.ofMillis(1300));assertEquals(0,layer.getChildCount());assertEquals("هل أنت مستعد؟",dialogue.getText().toString());
 }}
}
