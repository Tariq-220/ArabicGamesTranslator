package dev.arabicgames.tv;
import android.content.*;import android.media.tv.*;import android.os.*;import android.net.Uri;import android.view.*;import android.widget.*;import org.junit.*;import org.junit.runner.RunWith;import org.robolectric.*;import org.robolectric.annotation.*;import org.robolectric.shadow.api.Shadow;import java.util.*;import java.time.Duration;import static org.junit.Assert.*;
@RunWith(RobolectricTestRunner.class) @Config(sdk=35,shadows={TvHdmiResumeTest.Inputs.class,TvHdmiResumeTest.Info.class,TvHdmiResumeTest.Video.class})
public class TvHdmiResumeTest {
 @Implements(TvInputManager.class) public static class Inputs {
  static List<TvInputInfo> list=new ArrayList<>();static List<TvInputManager.TvInputCallback> callbacks=new ArrayList<>();
  @Implementation protected List<TvInputInfo> getTvInputList(){return new ArrayList<>(list);}
  @Implementation protected void registerCallback(TvInputManager.TvInputCallback c,Handler h){callbacks.add(c);}
  @Implementation protected void unregisterCallback(TvInputManager.TvInputCallback c){callbacks.remove(c);}
 }
 @Implements(TvInputInfo.class) public static class Info {
  @Implementation protected int getType(){return TvInputInfo.TYPE_HDMI;}
  @Implementation protected boolean isPassthroughInput(){return true;}
  @Implementation protected String getId(){return "tcl-HDMI-2";}
  @Implementation protected CharSequence loadLabel(Context c){return "HDMI 2";}
 }
 @Implements(TvView.class) public static class Video extends org.robolectric.shadows.ShadowViewGroup {
  static int tunes,resets;static String last;static TvView.TvInputCallback callback;
  @Implementation protected void setCallback(TvView.TvInputCallback c){callback=c;}
  @Implementation protected void tune(String id,Uri uri){tunes++;last=id;}
  @Implementation(methodName="reset") protected void resetSession(){resets++;}
  @Implementation protected void setStreamVolume(float v){}
 }
 private static Button button(View v,String label){if(v instanceof Button&&label.equals(((Button)v).getText().toString()))return(Button)v;if(v instanceof ViewGroup)for(int i=0;i<((ViewGroup)v).getChildCount();i++){Button b=button(((ViewGroup)v).getChildAt(i),label);if(b!=null)return b;}return null;}
 @Before public void setup(){Inputs.list.clear();Inputs.callbacks.clear();Video.tunes=Video.resets=0;Video.last=null;Video.callback=null;RuntimeEnvironment.getApplication().getSharedPreferences("MainActivity",0).edit().clear().commit();}
 private org.robolectric.android.controller.ActivityController<MainActivity> controller(){var c=Robolectric.buildActivity(MainActivity.class);android.content.Context base=c.get().getBaseContext();assertNotNull("Activity attached context",base);org.robolectric.shadows.ShadowContextImpl shadow=Shadow.extract(base);shadow.setSystemService(Context.TV_INPUT_SERVICE,Shadow.newInstanceOf(TvInputManager.class));c.setup();Shadows.shadowOf(Looper.getMainLooper()).idle();return c;}
 private void add(){Inputs.list.add(Shadow.newInstanceOf(TvInputInfo.class));for(var c:new ArrayList<>(Inputs.callbacks))c.onInputAdded("tcl-HDMI-2");}
 @Test public void homeReleasesTheOldSessionAndReturnRetunesTheSavedHdmi(){add();try(var c=controller()){
  button(c.get().getWindow().getDecorView(),"عرض اللعبة / إخفاء اللوحة").performClick();assertEquals(1,Video.tunes);c.pause().stop();assertTrue(Video.resets>0);c.start().resume();Shadows.shadowOf(Looper.getMainLooper()).idleFor(Duration.ofSeconds(5));assertEquals(2,Video.tunes);assertEquals("tcl-HDMI-2",Video.last);
 }}
 @Test public void gamePanelPauseDoesNotResetOrRetuneHdmi(){add();try(var c=controller()){button(c.get().getWindow().getDecorView(),"عرض اللعبة / إخفاء اللوحة").performClick();int resets=Video.resets;c.pause();assertEquals(resets,Video.resets);c.resume();Shadows.shadowOf(Looper.getMainLooper()).idleFor(Duration.ofSeconds(5));assertEquals(1,Video.tunes);}}
 @Test public void delayedInputListAfterRecreationRestoresWithoutDuplicatingEntries(){add();try(var c=controller()){button(c.get().getWindow().getDecorView(),"عرض اللعبة / إخفاء اللوحة").performClick();}Inputs.list.clear();try(var c=controller()){
  int before=Video.tunes;Shadows.shadowOf(Looper.getMainLooper()).idleFor(Duration.ofMillis(500));assertEquals(before,Video.tunes);add();Shadows.shadowOf(Looper.getMainLooper()).idleFor(Duration.ofSeconds(5));assertEquals(before+1,Video.tunes);assertEquals("tcl-HDMI-2",Video.last);
 }assertTrue(Inputs.callbacks.isEmpty());}
 @Test public void repeatedSystemPanelsNeverCreateAdditionalHdmiSessions(){add();try(var c=controller()){button(c.get().getWindow().getDecorView(),"عرض اللعبة / إخفاء اللوحة").performClick();int resets=Video.resets;for(int i=0;i<5;i++){c.pause().resume();Shadows.shadowOf(Looper.getMainLooper()).idleFor(Duration.ofSeconds(5));}assertEquals(1,Video.tunes);assertEquals(resets,Video.resets);}}
 @Test public void duplicateCaptionRenewalsDoNotRelayoutText()throws Exception{try(var c=controller()){MainActivity a=c.get();TextView subtitle=org.robolectric.util.ReflectionHelpers.getField(a,"subtitle");int[] changes={0};subtitle.addTextChangedListener(new android.text.TextWatcher(){public void beforeTextChanged(CharSequence s,int start,int count,int after){}public void onTextChanged(CharSequence s,int start,int before,int count){changes[0]++;}public void afterTextChanged(android.text.Editable e){}});for(int i=0;i<20;i++)org.robolectric.util.ReflectionHelpers.callInstanceMethod(a,"show",org.robolectric.util.ReflectionHelpers.ClassParameter.from(String.class,"نفس الترجمة"),org.robolectric.util.ReflectionHelpers.ClassParameter.from(int.class,5000));assertEquals(1,changes[0]);}}
 @Test public void dashboardSignalChangeWaitsThenRetunesAndStopsWhenVideoReturns(){add();try(var c=controller()){
  button(c.get().getWindow().getDecorView(),"عرض اللعبة / إخفاء اللوحة").performClick();Video.callback.onVideoAvailable("tcl-HDMI-2");
  TvView oldView=org.robolectric.util.ReflectionHelpers.getField(c.get(),"tv");Video.callback.onVideoUnavailable("tcl-HDMI-2",TvInputManager.VIDEO_UNAVAILABLE_REASON_UNKNOWN);
  Shadows.shadowOf(Looper.getMainLooper()).idleFor(Duration.ofSeconds(2));assertEquals(1,Video.tunes);
  Shadows.shadowOf(Looper.getMainLooper()).idleFor(Duration.ofSeconds(2));assertEquals(2,Video.tunes);assertNotSame(oldView,org.robolectric.util.ReflectionHelpers.getField(c.get(),"tv"));
  Video.callback.onVideoAvailable("tcl-HDMI-2");Shadows.shadowOf(Looper.getMainLooper()).idleFor(Duration.ofSeconds(15));assertEquals(2,Video.tunes);
 }}
 @Test public void shortFormatSwitchDoesNotRetuneOrReset(){add();try(var c=controller()){
  button(c.get().getWindow().getDecorView(),"عرض اللعبة / إخفاء اللوحة").performClick();int resets=Video.resets;
  Video.callback.onVideoUnavailable("tcl-HDMI-2",TvInputManager.VIDEO_UNAVAILABLE_REASON_BUFFERING);Video.callback.onVideoAvailable("tcl-HDMI-2");Shadows.shadowOf(Looper.getMainLooper()).idleFor(Duration.ofSeconds(10));assertEquals(1,Video.tunes);assertEquals(resets,Video.resets);
 }}
 @Test public void menuBoxPositionsNearSourceAndDialogueRestoresBottom()throws Exception{try(var c=controller()){
  var args=org.robolectric.util.ReflectionHelpers.ClassParameter.from(org.json.JSONObject.class,new org.json.JSONObject("{\"text\":\"الإعدادات\",\"box\":{\"left\":0.1,\"top\":0.2,\"right\":0.5,\"bottom\":0.4}}"));
  org.robolectric.util.ReflectionHelpers.callInstanceMethod(c.get(),"applyCaption",args,org.robolectric.util.ReflectionHelpers.ClassParameter.from(String.class,"test"));
  TextView caption=org.robolectric.util.ReflectionHelpers.getField(c.get(),"subtitle");FrameLayout.LayoutParams p=(FrameLayout.LayoutParams)caption.getLayoutParams();assertEquals(Gravity.TOP|Gravity.LEFT,p.gravity);assertTrue(p.topMargin>0);
  org.robolectric.util.ReflectionHelpers.callInstanceMethod(c.get(),"applyCaption",org.robolectric.util.ReflectionHelpers.ClassParameter.from(org.json.JSONObject.class,new org.json.JSONObject("{\"text\":\"حوار\",\"box\":{\"left\":0.2,\"top\":0.8,\"right\":0.8,\"bottom\":0.9}}")),org.robolectric.util.ReflectionHelpers.ClassParameter.from(String.class,"test"));assertEquals(Gravity.BOTTOM|Gravity.CENTER_HORIZONTAL,((FrameLayout.LayoutParams)caption.getLayoutParams()).gravity);
 }}
}
