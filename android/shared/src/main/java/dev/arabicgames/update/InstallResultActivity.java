// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames.update;
import android.app.Activity;import android.content.Intent;import android.content.pm.PackageInstaller;import android.os.Bundle;
public final class InstallResultActivity extends Activity {
 @Override public void onCreate(Bundle state){super.onCreate(state);int status=getIntent().getIntExtra(PackageInstaller.EXTRA_STATUS,PackageInstaller.STATUS_FAILURE);
  if(status==PackageInstaller.STATUS_PENDING_USER_ACTION){Intent prompt=getIntent().getParcelableExtra(Intent.EXTRA_INTENT);if(prompt!=null)try{startActivity(prompt);}catch(RuntimeException ignored){}}
  else android.widget.Toast.makeText(this,status==PackageInstaller.STATUS_SUCCESS?"تم تحديث التطبيق مع الاحتفاظ بإعداداته":"لم يكتمل التحديث؛ النسخة الحالية محفوظة.",android.widget.Toast.LENGTH_LONG).show();finish();}
}
