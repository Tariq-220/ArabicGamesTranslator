// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames.phone;
import android.app.Activity;
import android.content.pm.ActivityInfo;
import android.graphics.Insets;
import android.graphics.Bitmap;
import android.graphics.Canvas;
import android.view.*;
import android.widget.*;
import org.junit.*;
import org.junit.runner.RunWith;
import org.robolectric.*;
import org.robolectric.annotation.*;
import static org.junit.Assert.*;
@org.robolectric.annotation.GraphicsMode(org.robolectric.annotation.GraphicsMode.Mode.NATIVE)
@RunWith(RobolectricTestRunner.class)
@Config(sdk=35)
public class PhoneLayoutTest {
 @Test public void oledCoverDoesNotFinishActivityAndBackRestoresBrightness(){
  try(var c=Robolectric.buildActivity(XboxActivity.class).setup()){
   XboxActivity a=c.get();android.view.WindowManager.LayoutParams attrs=a.getWindow().getAttributes();attrs.screenBrightness=.7f;a.getWindow().setAttributes(attrs);
   a.enterDarkMode();android.app.AlertDialog dialog=org.robolectric.shadows.ShadowAlertDialog.getLatestAlertDialog();dialog.getButton(android.content.DialogInterface.BUTTON_POSITIVE).performClick();Shadows.shadowOf(android.os.Looper.getMainLooper()).idle();
   assertEquals(.01f,a.getWindow().getAttributes().screenBrightness,.001f);assertFalse(a.isFinishing());
   a.onBackPressed();assertEquals(.7f,a.getWindow().getAttributes().screenBrightness,.001f);assertFalse(a.isFinishing());
  }
 }
 private static void layout(Activity a,int width,int height){View decor=a.getWindow().getDecorView();decor.measure(View.MeasureSpec.makeMeasureSpec(width,View.MeasureSpec.EXACTLY),View.MeasureSpec.makeMeasureSpec(height,View.MeasureSpec.EXACTLY));decor.layout(0,0,width,height);}
 private static void preview(Activity a,String name){try{View v=a.getWindow().getDecorView();Bitmap image=Bitmap.createBitmap(v.getWidth(),v.getHeight(),Bitmap.Config.ARGB_8888);v.draw(new Canvas(image));java.io.File dir=new java.io.File("build/ui-previews");dir.mkdirs();try(java.io.FileOutputStream file=new java.io.FileOutputStream(new java.io.File(dir,name))){image.compress(Bitmap.CompressFormat.PNG,100,file);}image.recycle();}catch(Exception e){throw new AssertionError(e);}}
 private static Button find(View v,String text){if(v instanceof Button&&((Button)v).getText().toString().equals(text))return (Button)v;if(v instanceof ViewGroup)for(int i=0;i<((ViewGroup)v).getChildCount();i++){Button b=find(((ViewGroup)v).getChildAt(i),text);if(b!=null)return b;}return null;}
 @Test @Config(qualifiers="w360dp-h780dp-port")public void mainOpensWithoutForcingLandscape(){try(var c=Robolectric.buildActivity(MainActivity.class).setup()){MainActivity a=c.get();layout(a,360,780);assertEquals(ActivityInfo.SCREEN_ORIENTATION_UNSPECIFIED,a.getRequestedOrientation());assertNotNull(find(a.getWindow().getDecorView(),"افتح Xbox — بدون كاميرا"));preview(a,"phone-portrait.png");}}
 @Test @Config(qualifiers="w780dp-h360dp-land")public void gameButtonsFitBelowSystemBarsInLandscape(){try(var c=Robolectric.buildActivity(XboxActivity.class).setup()){
  XboxActivity a=c.get();ViewGroup content=a.findViewById(android.R.id.content);View root=content.getChildAt(0);root.dispatchApplyWindowInsets(new WindowInsets.Builder().setInsets(WindowInsets.Type.statusBars(),Insets.of(0,28,0,0)).setInsets(WindowInsets.Type.navigationBars(),Insets.of(0,0,0,20)).build());layout(a,780,360);
  Button start=find(root,"بدء الترجمة"),stop=find(root,"إيقاف"),sound=find(root,"صوت الجوال: مكتوم");assertNotNull(start);assertNotNull(stop);assertNotNull(sound);int[] xy=new int[2];start.getLocationInWindow(xy);assertTrue("Start button must clear status bar",xy[1]>=28);assertTrue(start.getWidth()>0);assertTrue(start.getRight()<=780);assertTrue(sound.getWidth()>0);assertFalse(stop.isEnabled());preview(a,"xbox-landscape.png");
  start.performClick();start.performClick();assertFalse(start.isEnabled());assertTrue(stop.isEnabled());
 }}

 private static TextView text(View v,String value){if(v instanceof TextView&&((TextView)v).getText().toString().equals(value))return (TextView)v;if(v instanceof ViewGroup)for(int i=0;i<((ViewGroup)v).getChildCount();i++){TextView t=text(((ViewGroup)v).getChildAt(i),value);if(t!=null)return t;}return null;}
 @Test @Config(qualifiers="w360dp-h780dp-port")public void arabicPreviewIsVisibleOnPhoneWithoutAnyTvConnection(){
  String arabic="آرثر: نحتاج إلى مزيد من المال.";
  try(var c=Robolectric.buildActivity(XboxActivity.class).setup()){
   Status.clients=0;Status.arabic=arabic;org.robolectric.Shadows.shadowOf(android.os.Looper.getMainLooper()).idleFor(java.time.Duration.ofMillis(350));layout(c.get(),360,780);
   TextView caption=text(c.get().getWindow().getDecorView(),arabic);assertNotNull(caption);assertEquals(View.VISIBLE,caption.getVisibility());
  }finally{Status.arabic="";}
 }
 @Test @Config(qualifiers="w360dp-h780dp-port")public void playStationSetupIsReadableInPortrait(){try(var c=Robolectric.buildActivity(PlayStationActivity.class).setup()){
  PlayStationActivity a=c.get();find(a.getWindow().getDecorView(),"ألعب على التلفزيون وبيد PS5").performClick();layout(a,360,780);preview(a,"ps5-secondary-account.png");
  find(a.getWindow().getDecorView(),"جهزت الحساب الثانوي على PS5 — التالي").performClick();layout(a,360,780);preview(a,"ps5-sony-stream.png");
  find(a.getWindow().getDecorView(),"ظهر فيديو اللعبة — التالي").performClick();find(a.getWindow().getDecorView(),"رجعت للأساسي واليد تعمل — التالي").performClick();layout(a,360,780);assertTrue(find(a.getWindow().getDecorView(),"بدء ترجمة PS5").getWidth()>0);preview(a,"ps5-start.png");
 }}

}
