// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames.phone;
import android.app.Activity;
import android.content.Intent;
import android.view.Gravity;
/** Settings stay in the same task, above the native/web player, with no new capture source. */
final class LiveTools {
 static final String PANEL="live-tools-panel";static final int REQUEST=909;
 static void open(Activity source,Class<? extends Activity> target){source.startActivityForResult(new Intent(source,target).putExtra(PANEL,true),REQUEST);}
 static void theme(Activity activity){if(activity.getIntent().getBooleanExtra(PANEL,false))activity.setTheme(dev.arabicgames.phone.R.style.LiveToolsPanel);}
 static void size(Activity activity){if(!activity.getIntent().getBooleanExtra(PANEL,false))return;android.util.DisplayMetrics m=activity.getResources().getDisplayMetrics();activity.getWindow().setLayout((int)(m.widthPixels*(m.widthPixels>m.heightPixels?.65:.94)),(int)(m.heightPixels*.94));activity.getWindow().setGravity(Gravity.END|Gravity.CENTER_VERTICAL);}
}
