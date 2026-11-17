package dev.arabicgames.phone;
import android.app.Activity;
import android.content.Intent;
import android.view.*;
import android.widget.*;
import org.junit.*;
import org.junit.runner.RunWith;
import org.robolectric.*;
import org.robolectric.annotation.Config;
import static org.junit.Assert.*;
@RunWith(RobolectricTestRunner.class) @Config(sdk=35)
public class PlayStationFlowTest {
 private static Button find(View v,String label){if(v instanceof Button&&label.equals(((Button)v).getText().toString()))return(Button)v;if(v instanceof ViewGroup)for(int i=0;i<((ViewGroup)v).getChildCount();i++){Button b=find(((ViewGroup)v).getChildAt(i),label);if(b!=null)return b;}return null;}
 private static String text(View v){String s=v instanceof TextView?((TextView)v).getText().toString():"";if(v instanceof ViewGroup)for(int i=0;i<((ViewGroup)v).getChildCount();i++)s+="\n"+text(((ViewGroup)v).getChildAt(i));return s;}
 @Test public void primaryHomeButtonOpensAnExplicitPlayStationFlow(){try(var c=Robolectric.buildActivity(MainActivity.class).setup()){
  find(c.get().getWindow().getDecorView(),"PS5 — ربط Sony وبدء الترجمة").performClick();assertEquals(PlayStationActivity.class.getName(),Shadows.shadowOf(c.get()).getNextStartedActivity().getComponent().getClassName());
 }}
 @Test public void tvModeExplainsSecondaryAccountButPhoneControlDoesNotRequireIt(){
  try(var c=Robolectric.buildActivity(PlayStationActivity.class).setup()){
   Activity a=c.get();find(a.getWindow().getDecorView(),"ألعب على التلفزيون وبيد PS5").performClick();assertTrue(text(a.getWindow().getDecorView()).contains("الثانوي"));c.recreate();assertTrue(text(c.get().getWindow().getDecorView()).contains("الثانوي"));
  }
  try(var c=Robolectric.buildActivity(PlayStationActivity.class).setup()){
   find(c.get().getWindow().getDecorView(),"ألعب وأتحكم من الجوال").performClick();assertTrue(text(c.get().getWindow().getDecorView()).contains("ليس مطلوبًا"));
  }
 }
 @Test public void permissionDenialOrMissingModelNeverStartsCapture(){boolean previous=Status.modelReady;Status.modelReady=false;
  try(var c=Robolectric.buildActivity(PlayStationActivity.class).setup()){
   PlayStationActivity a=c.get();find(a.getWindow().getDecorView(),"ألعب على التلفزيون وبيد PS5").performClick();find(a.getWindow().getDecorView(),"جهزت الحساب الثانوي على PS5 — التالي").performClick();find(a.getWindow().getDecorView(),"ظهر فيديو اللعبة — التالي").performClick();find(a.getWindow().getDecorView(),"رجعت للأساسي واليد تعمل — التالي").performClick();find(a.getWindow().getDecorView(),"بدء ترجمة PS5").performClick();assertNull(Shadows.shadowOf(a).getNextStartedActivityForResult());
   a.onActivityResult(701,Activity.RESULT_CANCELED,null);assertNull(Shadows.shadowOf(a).getNextStartedService());
  }finally{Status.modelReady=previous;}
 }
 @Test public void translatorUiLifecyclePausesExternalOcrWhileSettingsAreVisible(){
  try(var c=Robolectric.buildActivity(PlayStationActivity.class).setup()){
   assertTrue(Status.translatorUiVisible);c.pause();assertFalse(Status.translatorUiVisible);c.resume();assertTrue(Status.translatorUiVisible);
  }
 }
 @Test public void missingSonyAppCannotStartCaptureOfTheStoreOrAnotherApp(){boolean previous=Status.modelReady;Status.modelReady=true;
  try(var c=Robolectric.buildActivity(PlayStationActivity.class).setup()){
   PlayStationActivity a=c.get();find(a.getWindow().getDecorView(),"ألعب وأتحكم من الجوال").performClick();find(a.getWindow().getDecorView(),"ظهر فيديو اللعبة — التالي").performClick();find(a.getWindow().getDecorView(),"بدء ترجمة PS5").performClick();
   assertNull(Shadows.shadowOf(a).getNextStartedActivityForResult());assertNull(Shadows.shadowOf(a).getNextStartedService());assertTrue(text(a.getWindow().getDecorView()).contains("ثبّت PS Remote Play"));
  }finally{Status.modelReady=previous;}
 }

 @Test public void anAlreadyPairedSonyStreamCanGoStraightToTranslationWithoutAnotherLogin(){try(var c=Robolectric.buildActivity(PlayStationActivity.class).setup()){
  PlayStationActivity a=c.get();find(a.getWindow().getDecorView(),"البث مربوط مسبقًا — ابدأ الترجمة").performClick();assertNotNull(find(a.getWindow().getDecorView(),"بدء ترجمة PS5"));assertNull(Shadows.shadowOf(a).getNextStartedActivityForResult());assertNull(Shadows.shadowOf(a).getNextStartedService());
 }}

}
