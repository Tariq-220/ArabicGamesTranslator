// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames.phone;
import android.os.Looper;
import android.view.*;
import android.widget.*;
import org.junit.*;
import org.junit.runner.RunWith;
import org.robolectric.*;
import org.robolectric.annotation.Config;
import static org.junit.Assert.*;
@RunWith(RobolectricTestRunner.class) @Config(sdk=35)
public class AiSettingsTest {
 @Test public void sessionKeyWorksWithoutWritingSecretToPreferences()throws Exception{
  android.content.Context context=org.robolectric.RuntimeEnvironment.getApplication();AiPreferences.clear(context);
  try{AiPreferences.useForSession("gemini","gemini-3.5-flash-lite","RDR2","session-secret",true,true);
   assertEquals("session-secret",AiPreferences.read(context).key);assertTrue(AiPreferences.cloud(context));
   assertFalse(AiPreferences.prefs(context).contains("gemini.key"));
   AiPreferences.clearSession();assertEquals("local",AiPreferences.provider(context));
  }finally{AiPreferences.clear(context);}
 }
 private static void idle(){Shadows.shadowOf(Looper.getMainLooper()).idle();}
 private static Button button(View v,String label){if(v instanceof Button&&((Button)v).getText().toString().equals(label))return (Button)v;if(v instanceof ViewGroup)for(int i=0;i<((ViewGroup)v).getChildCount();i++){Button b=button(((ViewGroup)v).getChildAt(i),label);if(b!=null)return b;}return null;}
 @Test public void duplicateSelectionAndProviderSwitchDoNotLoseTypedKey(){
  try(var c=Robolectric.buildActivity(AiSettingsActivity.class).setup()){
   idle();View root=c.get().getWindow().getDecorView();Spinner provider=root.findViewWithTag("api-provider");EditText key=root.findViewWithTag("api-key");
   provider.setSelection(1);idle();key.setText("gemini-test-secret");
   provider.getOnItemSelectedListener().onItemSelected(provider,null,1,1);assertEquals("gemini-test-secret",key.getText().toString());
   provider.setSelection(2);idle();assertEquals("",key.getText().toString());key.setText("openai-test-secret");
   provider.setSelection(1);idle();assertEquals("gemini-test-secret",key.getText().toString());
  }
 }
 @Test public void failedSavePreservesTypedKeyAndDoesNotChangeActiveProvider(){
  try(var c=Robolectric.buildActivity(AiSettingsActivity.class).setup()){
   idle();View root=c.get().getWindow().getDecorView();Spinner provider=root.findViewWithTag("api-provider");provider.setSelection(1);idle();
   EditText key=root.findViewWithTag("api-key");key.setText("test-secret");button(root,"حفظ المحرك والعودة").performClick();
   assertNotNull(org.robolectric.shadows.ShadowAlertDialog.getLatestAlertDialog());
   assertEquals("test-secret",key.getText().toString());assertFalse(c.get().isFinishing());assertEquals("local",AiPreferences.provider(c.get()));
  }
 }
 @Test public void agreeingWithEmptyKeyShowsValidationWithoutChangingProvider(){
  try(var c=Robolectric.buildActivity(AiSettingsActivity.class).setup()){
   idle();View root=c.get().getWindow().getDecorView();Spinner provider=root.findViewWithTag("api-provider");provider.setSelection(1);idle();
   button(root,"حفظ المحرك والعودة").performClick();android.app.AlertDialog dialog=org.robolectric.shadows.ShadowAlertDialog.getLatestAlertDialog();
   dialog.getButton(android.content.DialogInterface.BUTTON_POSITIVE).performClick();
   assertEquals("local",AiPreferences.provider(c.get()));assertFalse(c.get().isFinishing());
  }
 }
 @Test public void modelsCanBeSelectedWithoutTypingAndTypedKeysSurvive(){
  try(var c=Robolectric.buildActivity(AiSettingsActivity.class).setup()){
   idle();View root=c.get().getWindow().getDecorView();Spinner provider=root.findViewWithTag("api-provider");provider.setSelection(1);idle();
   Spinner choices=root.findViewWithTag("model-choice");EditText model=root.findViewWithTag("api-model"),key=root.findViewWithTag("api-key");key.setText("gemini-draft");
   choices.setSelection(1);idle();assertEquals("gemini-3.8-flash",model.getText().toString());assertEquals(View.GONE,model.getVisibility());assertEquals("gemini-draft",key.getText().toString());
   provider.setSelection(2);idle();assertTrue(choices.getItemAtPosition(0).toString().startsWith("gpt-"));
   provider.setSelection(1);idle();assertEquals("gemini-draft",key.getText().toString());assertEquals("gemini-3.8-flash",model.getText().toString());
   c.recreate();idle();root=c.get().getWindow().getDecorView();model=root.findViewWithTag("api-model");key=root.findViewWithTag("api-key");assertEquals("gemini-3.8-flash",model.getText().toString());assertEquals("gemini-draft",key.getText().toString());
  }
 }
 @Test public void apiShortcutDoesNotCollapseProviderSpinner(){
  try(var c=Robolectric.buildActivity(AiSettingsActivity.class).setup()){
   idle();View root=c.get().getWindow().getDecorView();root.measure(View.MeasureSpec.makeMeasureSpec(360,View.MeasureSpec.EXACTLY),View.MeasureSpec.makeMeasureSpec(780,View.MeasureSpec.EXACTLY));root.layout(0,0,360,780);
   Spinner provider=root.findViewWithTag("api-provider");assertTrue("Provider selector must remain visible beside API shortcut",provider.getWidth()>100);
   Spinner models=root.findViewWithTag("model-choice");assertTrue(models.getWidth()>200);
  }
 }
 @Test public void clipboardWhitespaceIsAcceptedButInternalWhitespaceRejected(){
  assertEquals("example-key",AiPreferences.validateInput("gemini","gemini-3.5-flash-lite"," \nexample-key\r\n",true));
  try{AiPreferences.validateInput("gemini","gemini-3.5-flash-lite","example\nkey",true);fail();}catch(IllegalArgumentException expected){assertFalse(expected.getMessage().contains("example"));}
 }
}
