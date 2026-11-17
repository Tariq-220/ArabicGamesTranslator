// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames.phone;

import android.app.Activity;
import android.content.*;
import android.media.projection.*;
import android.net.Uri;
import android.os.*;
import android.view.*;
import android.widget.*;

/** Alternative sources. PS5 uses a user-approved external capture, not a native PS client. */
public final class SourcePathsActivity extends Activity {
 private TextView feedback;
 @Override public void onCreate(Bundle saved){
  super.onCreate(saved);
  getWindow().setStatusBarColor(Ui.BG);getWindow().setNavigationBarColor(Ui.BG);
  LinearLayout root=Ui.column(this);root.setBackgroundColor(Ui.BG);
  root.setPadding(Ui.dp(this,16),Ui.dp(this,16),Ui.dp(this,16),Ui.dp(this,20));
  root.addView(Ui.text(this,"مصادر اللعبة",26));
  LinearLayout pc=Ui.card(this,root,"الكمبيوتر — الجوال يقدر ينقفل");
  pc.addView(Ui.text(this,"Windows 10/11 بنظام 64 بت. شغّل حزمة الكمبيوتر المرفقة، وافتح Xbox Remote Play أو PS Remote Play أو chiaki-ng، ثم اختر نافذة اللعبة في المترجم. أبقِ النافذة ظاهرة والكمبيوتر مستيقظًا طوال اللعب.",15));
  pc.addView(Ui.text(this,"على التلفزيون: فعّل «مصدر كمبيوتر GameTranslator / CouchSubs»، وأدخل عنوان الكمبيوتر المحلي. المنفذ 8787؛ رمز الجوال غير مستخدم. العب عبر HDMI واسمع الصوت من الشاشة. بعد وصول العربية من الكمبيوتر تقدر تقفل الجوال.",15));
  Ui.button(this,pc,"إيقاف مصدر الجوال قبل استخدام الكمبيوتر",false,()->{
   stopService(new Intent(this,CaptureService.class));
   feedback.setText("تم إيقاف مصدر الجوال. حوّل ربط التلفزيون إلى عنوان الكمبيوتر وخيار مصدر كمبيوتر.");
  });
  Ui.button(this,pc,"تنزيل PS Remote Play للكمبيوتر — Sony",false,()->open("https://www.playstation.com/en-us/remote-play/"));
  Ui.button(this,pc,"Xbox Remote Play الرسمي",false,()->open("https://www.xbox.com/remoteplay"));
  Ui.button(this,pc,"chiaki-ng — بديل PS5 للكمبيوتر",false,()->open("https://github.com/chiaki-ng/chiaki-ng/releases"));
  pc.addView(Ui.text(this,"Gemini متاح في حزمة الكمبيوتر: أدخل مفتاحك داخل البرنامج فقط، ثم فعّل استخدام Gemini. يحتاج الإنترنت وقد تترتب تكلفة أو حدود استخدام. البديل العام الحالي تجريبي وقد يتوقف.",14));
  LinearLayout ps=Ui.card(this,root,"PS5 على الجوال — مشاركة شاشة تجريبية");
  ps.addView(Ui.text(this,"1. على PS5: الإعدادات ← النظام ← اللعب عن بُعد ← تمكين.\n2. افتح PS Remote Play وسجّل الدخول لدى Sony، واتصل حتى تظهر اللعبة.\n3. ارجع هنا وابدأ مشاركة الشاشة، ثم ارجع إلى PS Remote Play. جهّز نموذج العربية من الصفحة الرئيسية أولًا.",15));
  ps.addView(Ui.text(this,"الجوال يبقى مفتوحًا في هذا المسار. قد يمنع PS Remote Play التقاط الفيديو فتكون القراءة سوداء؛ وقتها جرّب مسار الكمبيوتر. هذا ليس اتصال PS5 أصليًا داخل تطبيقنا، ولم يُختبر على جهاز PS5 فعلي.",14));
  Ui.button(this,ps,"فتح / تثبيت PS Remote Play",true,this::openPlayStation);
  Ui.button(this,ps,"بدء مشاركة شاشة PS5",false,this::requestCapture);
  LinearLayout obs=Ui.card(this,root,"بديل OBS / بطاقة التقاط");
  obs.addView(Ui.text(this,"على الكمبيوتر يمكن اختيار نافذة معاينة OBS. إذا توفرت بطاقة التقاط، أضفها في OBS واختر المعاينة في المترجم. لا تحتاج شراء بطاقة لتجربة Remote Play. لا يوجد تجاوز للمحتوى المحمي.",15));
  feedback=Ui.text(this,"لا نغيّر مصدر التلفزيون تلقائيًا؛ اختر مسارًا واحدًا للتجربة.",14);root.addView(feedback);
  Ui.button(this,root,"رجوع",false,this::finish);
  ScrollView scroll=new ScrollView(this);scroll.addView(root);PhoneInsets.fit(scroll);setContentView(scroll);
 }
 private void open(String url){try{startActivity(new Intent(Intent.ACTION_VIEW,Uri.parse(url)));}catch(ActivityNotFoundException e){feedback.setText("لا يوجد متصفح لفتح الرابط.");}}
 private void openPlayStation(){
  Intent launch=getPackageManager().getLaunchIntentForPackage("com.playstation.remoteplay");
  if(launch!=null){try{startActivity(launch);return;}catch(ActivityNotFoundException ignored){}}
  open("https://play.google.com/store/apps/details?id=com.playstation.remoteplay");
 }
 private void requestCapture(){
  if(!AiPreferences.canTranslate(this)){feedback.setText("جهّز النموذج المحلي أو اختر AI من إعدادات المحرك، ثم ارجع لمشاركة الشاشة.");return;}
  MediaProjectionManager manager=(MediaProjectionManager)getSystemService(MEDIA_PROJECTION_SERVICE);
  try{
   Intent permission=Build.VERSION.SDK_INT>=34?manager.createScreenCaptureIntent(MediaProjectionConfig.createConfigForDefaultDisplay()):manager.createScreenCaptureIntent();
   startActivityForResult(permission,701);
  }catch(Exception e){feedback.setText("مشاركة الشاشة غير متاحة على هذا الجهاز. جرّب مسار الكمبيوتر.");}
 }
 @Override protected void onActivityResult(int request,int result,Intent data){
  super.onActivityResult(request,result,data);if(request!=701)return;
  if(result!=RESULT_OK||data==null){feedback.setText("لم تبدأ المشاركة؛ لم يُمنح إذن النظام.");return;}
  startForegroundService(new Intent(this,CaptureService.class).setAction("capture").putExtra("result",result).putExtra("data",data));
  feedback.setText("بدأت المشاركة. انتقل إلى PS Remote Play واترك شاشة اللعبة ظاهرة؛ إن كان الفيديو أسود جرّب الكمبيوتر.");
 }
}
