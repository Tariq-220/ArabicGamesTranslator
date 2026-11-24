package dev.arabicgames.tv;
import android.media.tv.TvView;
import android.view.*;
import android.widget.*;
import org.junit.*;
import org.junit.runner.RunWith;
import org.robolectric.*;
import org.robolectric.annotation.Config;
import static org.junit.Assert.*;
@RunWith(RobolectricTestRunner.class) @Config(sdk=35,qualifiers="w960dp-h540dp-land-television")
public class TvRemoteControlTest {
 private static Button find(View v,String label){if(v instanceof Button&&label.equals(((Button)v).getText().toString()))return(Button)v;if(v instanceof ViewGroup)for(int i=0;i<((ViewGroup)v).getChildCount();i++){Button b=find(((ViewGroup)v).getChildAt(i),label);if(b!=null)return b;}return null;}
 private static void key(MainActivity a,int code){assertTrue(a.dispatchKeyEvent(new KeyEvent(KeyEvent.ACTION_DOWN,code)));assertTrue(a.dispatchKeyEvent(new KeyEvent(KeyEvent.ACTION_UP,code)));}
 @Test public void hdmiCannotTakeNavigationAndKeyUpDoesNotActivateAHiddenButton(){
  try(var c=Robolectric.buildActivity(MainActivity.class).setup()){
   MainActivity a=c.get();View root=a.getWindow().getDecorView();TvView hdmi=root.findViewWithTag("hdmi-view");Button handle=root.findViewWithTag("menu-handle");assertFalse(hdmi.isFocusable());assertEquals(ViewGroup.FOCUS_BLOCK_DESCENDANTS,hdmi.getDescendantFocusability());
   key(a,KeyEvent.KEYCODE_BACK);assertEquals(View.GONE,handle.getVisibility());
   key(a,KeyEvent.KEYCODE_DPAD_CENTER);assertEquals(View.GONE,handle.getVisibility());assertTrue(find(root,"اكتشاف الجوال أو الكمبيوتر").isFocused());
   key(a,KeyEvent.KEYCODE_MENU);assertEquals(View.GONE,handle.getVisibility());
   key(a,KeyEvent.KEYCODE_DPAD_DOWN);assertEquals(View.GONE,handle.getVisibility());
   key(a,KeyEvent.KEYCODE_BACK);key(a,KeyEvent.KEYCODE_INFO);assertEquals(View.GONE,handle.getVisibility());
  }
 }
 @Test public void noHdmiInputDoesNotHideTheOnlyUsableControls(){try(var c=Robolectric.buildActivity(MainActivity.class).setup()){
  View root=c.get().getWindow().getDecorView();find(root,"عرض اللعبة عبر HDMI").performClick();assertEquals(View.GONE,root.findViewWithTag("menu-handle").getVisibility());
 }}
}
