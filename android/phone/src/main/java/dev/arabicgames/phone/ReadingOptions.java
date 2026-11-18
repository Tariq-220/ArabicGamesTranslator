// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames.phone;
import android.app.*;import android.content.Context;
final class ReadingOptions {
 static final String[] MODES={"subtitles","all","left","right","phoneRight","phoneLeft"};
 static void select(Context c,int which){if(which<0||which>=MODES.length)throw new IllegalArgumentException();c.getSharedPreferences("reading",0).edit().putString("region",MODES[which]).putBoolean("dialogueOnly",which==0).apply();CaptureService.readingChanged();Status.message="تغيّرت منطقة القراءة؛ الترجمة في شريط النص. ارجع للحوار بعد إغلاق الرسائل أو القوائم.";}
 static void show(Activity a){int selected=java.util.Arrays.asList(MODES).indexOf(a.getSharedPreferences("reading",0).getString("region","subtitles"));new AlertDialog.Builder(a).setTitle("ماذا تريد ترجمته؟").setSingleChoiceItems(new String[]{"الحوار أسفل الصورة","القوائم / كامل فيديو اللعبة","قائمة يسار الفيديو","قائمة يمين الفيديو","رسائل هاتف اللعبة — يمين","رسائل هاتف اللعبة — يسار"},Math.max(0,selected),(dialog,which)->{select(a,which);dialog.dismiss();}).setNegativeButton("إلغاء",null).show();}
}
