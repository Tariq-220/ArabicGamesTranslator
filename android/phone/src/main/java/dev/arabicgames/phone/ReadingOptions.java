// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames.phone;
import android.app.*;import android.content.Context;
final class ReadingOptions {
 static final String[] MODES={"subtitles","all","left","right","phoneRight","phoneLeft"};
 static void select(Context c,int which){if(which<0||which>=MODES.length)throw new IllegalArgumentException();c.getSharedPreferences("reading",0).edit().putString("region",MODES[which]).putBoolean("dialogueOnly",which==0).apply();CaptureService.readingChanged();Status.message="تغيّرت منطقة القراءة؛ الترجمة في شريط النص. ارجع للحوار بعد إغلاق الرسائل أو القوائم.";}
 static void show(Activity a){LiveTools.open(a,ReadingCardsActivity.class);}
}
