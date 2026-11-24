// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames.tv;
import android.media.tv.*;import android.os.*;import android.view.*;import android.widget.*;
import org.junit.*;import org.junit.runner.RunWith;import org.robolectric.*;import org.robolectric.annotation.*;import java.time.Duration;import static org.junit.Assert.*;
@RunWith(RobolectricTestRunner.class)
@Config(sdk=34,shadows={TvHdmiResumeTest.Inputs.class,TvHdmiResumeTest.Info.class,TvHdmiResumeTest.Video.class})
public class TvRebuildLifecycleTest {
 private static Button button(View v,String label){if(v instanceof Button&&label.equals(((Button)v).getText().toString()))return(Button)v;if(v instanceof ViewGroup)for(int i=0;i<((ViewGroup)v).getChildCount();i++){Button b=button(((ViewGroup)v).getChildAt(i),label);if(b!=null)return b;}return null;}
 @Before public void reset(){new TvHdmiResumeTest().setup();TvHdmiResumeTest.Inputs.list.add(org.robolectric.shadow.api.Shadow.newInstanceOf(TvInputInfo.class));}
 @Test public void HdrTransitionAndActivityStopDoNotRetuneOrOpenExternalHdmi(){
  try(var c=Robolectric.buildActivity(MainActivity.class).setup()){
   Button play=button(c.get().getWindow().getDecorView(),"عرض اللعبة عبر HDMI");assertNotNull(play);play.performClick();
   var video=TvHdmiResumeTest.Video.callback;video.onVideoAvailable("tcl-HDMI-2");int resets=TvHdmiResumeTest.Video.resets;
   video.onVideoUnavailable("tcl-HDMI-2",TvInputManager.VIDEO_UNAVAILABLE_REASON_UNKNOWN);
   Shadows.shadowOf(Looper.getMainLooper()).idleFor(Duration.ofSeconds(15));assertEquals(1,TvHdmiResumeTest.Video.tunes);
   c.pause().stop().start().resume();assertEquals(resets,TvHdmiResumeTest.Video.resets);assertEquals(1,TvHdmiResumeTest.Video.tunes);
   assertNull(Shadows.shadowOf(c.get()).getNextStartedActivity());
  }
 }
 @Test public void RepairConnectionIsSeparateFromHdmiRepair(){
  try(var c=Robolectric.buildActivity(MainActivity.class).setup()){
   assertNotNull(button(c.get().getWindow().getDecorView(),"إصلاح الاتصال"));
   assertNotNull(button(c.get().getWindow().getDecorView(),"إصلاح صورة HDMI السوداء"));
   assertNotNull(button(c.get().getWindow().getDecorView(),"عودة للعب"));
  }
 }
}
