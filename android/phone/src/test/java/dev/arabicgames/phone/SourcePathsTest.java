// SPDX-License-Identifier: AGPL-3.0-only
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
public class SourcePathsTest {
 private static Button button(View view,String label){
  if(view instanceof Button && ((Button)view).getText().toString().equals(label))return (Button)view;
  if(view instanceof ViewGroup)for(int i=0;i<((ViewGroup)view).getChildCount();i++){
   Button found=button(((ViewGroup)view).getChildAt(i),label);if(found!=null)return found;
  }return null;
 }
 @Test public void mainRoutesToAlternativeSources(){
  try(var controller=Robolectric.buildActivity(MainActivity.class).setup()){
   MainActivity a=controller.get();button(a.getWindow().getDecorView(),"PS5 / الكمبيوتر / OBS — طرق أخرى").performClick();
   Intent next=Shadows.shadowOf(a).getNextStartedActivity();assertEquals(SourcePathsActivity.class.getName(),next.getComponent().getClassName());
  }
 }
 @Test public void missingPsAppOpensOfficialStoreAndDoesNotStartCapture(){
  try(var controller=Robolectric.buildActivity(SourcePathsActivity.class).setup()){
   SourcePathsActivity a=controller.get();button(a.getWindow().getDecorView(),"فتح / تثبيت PS Remote Play").performClick();
   Intent next=Shadows.shadowOf(a).getNextStartedActivity();assertEquals("https://play.google.com/store/apps/details?id=com.playstation.remoteplay",next.getDataString());
   assertNull(Shadows.shadowOf(a).getNextStartedService());
  }
 }
 @Test public void noCaptureBeforeModelOrAfterPermissionDenied(){
  boolean previous=Status.modelReady;Status.modelReady=false;
  try(var controller=Robolectric.buildActivity(SourcePathsActivity.class).setup()){
   SourcePathsActivity a=controller.get();button(a.getWindow().getDecorView(),"بدء مشاركة شاشة PS5").performClick();
   assertNull(Shadows.shadowOf(a).getNextStartedActivityForResult());
   a.onActivityResult(701,Activity.RESULT_CANCELED,null);
   assertNull(Shadows.shadowOf(a).getNextStartedService());
  }finally{Status.modelReady=previous;}
 }
}
