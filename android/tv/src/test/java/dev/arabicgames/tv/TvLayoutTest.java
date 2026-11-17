// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames.tv;
import android.view.*;
import android.widget.*;
import org.junit.Test;
import org.junit.runner.RunWith;
import org.robolectric.*;
import org.robolectric.annotation.*;
import static org.junit.Assert.*;
@org.robolectric.annotation.GraphicsMode(org.robolectric.annotation.GraphicsMode.Mode.NATIVE)
@RunWith(RobolectricTestRunner.class)
@Config(sdk=35,qualifiers="w960dp-h540dp-land-television")
public class TvLayoutTest {
 @Test public void subtitleSizeCanBeChangedAndSurvivesRecreation(){
  try(var c=Robolectric.buildActivity(MainActivity.class).setup()){
   MainActivity a=c.get();ViewGroup root=(ViewGroup)((ViewGroup)a.findViewById(android.R.id.content)).getChildAt(0);TextView caption=(TextView)root.getChildAt(1);
   assertEquals(20f,caption.getTextSize()/a.getResources().getDisplayMetrics().scaledDensity,.1f);assertEquals(-2,caption.getLayoutParams().width);
   find(root,"حجم وخلفية الترجمة").performClick();android.app.AlertDialog dialog=org.robolectric.shadows.ShadowAlertDialog.getLatestAlertDialog();
   SeekBar size=dialog.getWindow().getDecorView().findViewWithTag("subtitle-size");assertNotNull(size);size.setProgress(3);
   assertEquals(17f,caption.getTextSize()/a.getResources().getDisplayMetrics().scaledDensity,.1f);
   dialog.dismiss();c.recreate();
   root=(ViewGroup)((ViewGroup)c.get().findViewById(android.R.id.content)).getChildAt(0);caption=(TextView)root.getChildAt(1);
   assertEquals(17f,caption.getTextSize()/c.get().getResources().getDisplayMetrics().scaledDensity,.1f);
  }
 }
 @Test public void subtitlePositionPersistsAndRepairRestoresVisibleDefaults(){
  try(var c=Robolectric.buildActivity(MainActivity.class).setup()){
   MainActivity a=c.get();ViewGroup root=(ViewGroup)((ViewGroup)a.findViewById(android.R.id.content)).getChildAt(0);
   find(root,"حجم وخلفية الترجمة").performClick();android.app.AlertDialog dialog=org.robolectric.shadows.ShadowAlertDialog.getLatestAlertDialog();
   SeekBar position=dialog.getWindow().getDecorView().findViewWithTag("subtitle-position");position.setProgress(18);dialog.dismiss();
   TextView caption=(TextView)root.getChildAt(1);int h=a.getResources().getDisplayMetrics().heightPixels;
   assertEquals(Math.round(h*.20f),((FrameLayout.LayoutParams)caption.getLayoutParams()).bottomMargin);
   c.recreate();a=c.get();root=(ViewGroup)((ViewGroup)a.findViewById(android.R.id.content)).getChildAt(0);caption=(TextView)root.getChildAt(1);
   assertEquals(Math.round(h*.20f),((FrameLayout.LayoutParams)caption.getLayoutParams()).bottomMargin);
   find(root,"إصلاح ظهور الترجمة").performClick();
   assertEquals(Math.round(h*.07f),((FrameLayout.LayoutParams)caption.getLayoutParams()).bottomMargin);
   assertEquals(View.VISIBLE,caption.getVisibility());assertTrue(caption.getText().toString().contains("اختبار الإصلاح"));
  }
 }
 private static Button find(View v,String text){if(v instanceof Button&&((Button)v).getText().toString().equals(text))return (Button)v;if(v instanceof ViewGroup)for(int i=0;i<((ViewGroup)v).getChildCount();i++){Button b=find(((ViewGroup)v).getChildAt(i),text);if(b!=null)return b;}return null;}
 @Test public void panelStaysNarrowAndTestSubtitleCanShow(){try(var c=Robolectric.buildActivity(MainActivity.class).setup()){
  MainActivity a=c.get();View decor=a.getWindow().getDecorView();int w=a.getResources().getDisplayMetrics().widthPixels,h=a.getResources().getDisplayMetrics().heightPixels;decor.measure(View.MeasureSpec.makeMeasureSpec(w,View.MeasureSpec.EXACTLY),View.MeasureSpec.makeMeasureSpec(h,View.MeasureSpec.EXACTLY));decor.layout(0,0,w,h);
  ViewGroup root=(ViewGroup)((ViewGroup)a.findViewById(android.R.id.content)).getChildAt(0);ScrollView panel=null;for(int i=0;i<root.getChildCount();i++)if(root.getChildAt(i) instanceof ScrollView)panel=(ScrollView)root.getChildAt(i);
  assertEquals(View.GONE,root.findViewWithTag("tv-settings").getVisibility());
  find(root,"الإعدادات: الربط والخط والصوت").performClick();assertEquals(View.VISIBLE,root.findViewWithTag("tv-settings").getVisibility());decor.measure(View.MeasureSpec.makeMeasureSpec(w,View.MeasureSpec.EXACTLY),View.MeasureSpec.makeMeasureSpec(h,View.MeasureSpec.EXACTLY));decor.layout(0,0,w,h);
  android.graphics.Rect visible=new android.graphics.Rect();assertTrue("Return-to-game control must be visible",find(root,"عرض اللعبة / إخفاء اللوحة").getGlobalVisibleRect(visible));
  try{android.graphics.Bitmap image=android.graphics.Bitmap.createBitmap(w,h,android.graphics.Bitmap.Config.ARGB_8888);decor.draw(new android.graphics.Canvas(image));java.io.File dir=new java.io.File("build/ui-previews");dir.mkdirs();try(java.io.FileOutputStream file=new java.io.FileOutputStream(new java.io.File(dir,"tv-panel.png"))){image.compress(android.graphics.Bitmap.CompressFormat.PNG,100,file);}image.recycle();}catch(Exception e){throw new AssertionError(e);}
  assertNotNull(panel);assertTrue(panel.getWidth()<=w*.46+1);assertTrue(panel.getHeight()<=h*.76+1);
  a.onKeyDown(KeyEvent.KEYCODE_PROG_RED,new KeyEvent(KeyEvent.ACTION_DOWN,KeyEvent.KEYCODE_PROG_RED));TextView caption=(TextView)root.getChildAt(1);assertEquals(View.VISIBLE,caption.getVisibility());assertTrue(caption.getText().toString().contains("اختبار العربية"));
 }}
}
