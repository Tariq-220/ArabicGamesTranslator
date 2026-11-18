package dev.arabicgames.phone;
import android.view.*;import android.widget.*;import org.junit.*;import org.junit.runner.RunWith;import org.robolectric.*;import org.robolectric.annotation.*;import org.robolectric.util.ReflectionHelpers;import static org.junit.Assert.*;
@RunWith(RobolectricTestRunner.class) @Config(sdk=35)
public class PlayStationReadingUiTest {
 private Button find(View v,String text){if(v instanceof Button&&text.contentEquals(((Button)v).getText()))return(Button)v;if(v instanceof ViewGroup)for(int i=0;i<((ViewGroup)v).getChildCount();i++){Button b=find(((ViewGroup)v).getChildAt(i),text);if(b!=null)return b;}return null;}
 @Test public void nativePlayStationReadingOptionsExistOnThePlayingStepAndBackCannotUnderflow(){try(var c=Robolectric.buildActivity(NativePlayStationActivity.class).setup()){assertNull(find(c.get().getWindow().getDecorView(),"الخطوة السابقة"));ReflectionHelpers.setField(c.get(),"step",4);ReflectionHelpers.callInstanceMethod(c.get(),"render");assertNotNull(find(c.get().getWindow().getDecorView(),"منطقة القراءة — الحوار / القوائم / رسائل الهاتف"));}}
 @Test public void externalPlayStationFirstStepHasReadingOptionsWithoutPreviousStep(){try(var c=Robolectric.buildActivity(PlayStationActivity.class).setup()){assertNull(find(c.get().getWindow().getDecorView(),"الخطوة السابقة"));assertNotNull(find(c.get().getWindow().getDecorView(),"منطقة القراءة — الحوار / القوائم / رسائل الهاتف"));}}
}
