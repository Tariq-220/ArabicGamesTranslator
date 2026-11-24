// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames.tv;
import android.os.Looper;
import android.view.View;
import android.widget.TextView;
import org.json.JSONObject;
import org.junit.Test;
import org.junit.runner.RunWith;
import org.robolectric.RobolectricTestRunner;
import org.robolectric.RuntimeEnvironment;
import org.robolectric.Shadows;
import org.robolectric.annotation.Config;
import java.time.Duration;
import static org.junit.Assert.*;
@RunWith(RobolectricTestRunner.class) @Config(sdk=34)
public class AreaCaptionLayerTest {
 private JSONObject caption(String id,int revision,String text)throws Exception{return new JSONObject("{\"id\":\""+id+"\",\"revision\":"+revision+",\"text\":\""+text+"\",\"duration\":2000,\"output\":{\"left\":0.1,\"top\":0.2,\"right\":0.5,\"bottom\":0.6}}");}
 @Test public void IndependentAreasRejectLateRevisionAndExpireSeparately()throws Exception{
  AreaCaptionLayer layer=new AreaCaptionLayer(RuntimeEnvironment.getApplication());layer.layout(0,0,1920,1080);
  layer.apply(caption("phone",2,"رسالة جديدة"));layer.apply(caption("menu",1,"إعدادات"));layer.apply(caption("phone",1,"رسالة قديمة"));
  assertEquals(2,layer.getChildCount());assertEquals("رسالة جديدة",((TextView)layer.getChildAt(0)).getText().toString());
  Shadows.shadowOf(Looper.getMainLooper()).idleFor(Duration.ofMillis(1200));layer.apply(caption("menu",2,"قائمة جديدة"));
  Shadows.shadowOf(Looper.getMainLooper()).idleFor(Duration.ofMillis(900));assertEquals(View.INVISIBLE,layer.getChildAt(0).getVisibility());assertEquals(View.VISIBLE,layer.getChildAt(1).getVisibility());
 }
 @Test public void ReconnectionClearsOldRevisionHistory()throws Exception{
  AreaCaptionLayer layer=new AreaCaptionLayer(RuntimeEnvironment.getApplication());layer.layout(0,0,1280,720);layer.apply(caption("phone",20,"سابق"));layer.clearAll();layer.apply(caption("phone",1,"جلسة جديدة"));
  assertEquals(1,layer.getChildCount());assertEquals("جلسة جديدة",((TextView)layer.getChildAt(0)).getText().toString());
 }
}
