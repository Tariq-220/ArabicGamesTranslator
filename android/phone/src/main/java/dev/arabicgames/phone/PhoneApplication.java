// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames.phone;
import android.app.*;
import android.os.Bundle;
/** Do not OCR our own settings/credentials when external screen capture is active. */
public final class PhoneApplication extends Application {
 @Override public void onCreate(){super.onCreate();registerActivityLifecycleCallbacks(new ActivityLifecycleCallbacks(){
  public void onActivityResumed(Activity a){Status.translatorUiVisible=true;}
  public void onActivityPaused(Activity a){Status.translatorUiVisible=false;}
  public void onActivityCreated(Activity a,Bundle b){}public void onActivityStarted(Activity a){}public void onActivityStopped(Activity a){}public void onActivitySaveInstanceState(Activity a,Bundle b){}public void onActivityDestroyed(Activity a){}
 });}
}
