// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames.phone;
import android.app.*;
import android.content.*;
import android.media.projection.MediaProjectionManager;
import android.net.Uri;
import android.os.*;
import android.widget.*;

/** Explicit external Sony app workflow; never claims native PS5 login/video access. */
public final class PlayStationActivity extends Activity {
 private int step;private boolean onTv=true;private LinearLayout page;private TextView live,feedback;
 private final Handler ui=new Handler(Looper.getMainLooper());
 private final Runnable refresh=new Runnable(){public void run(){if(live!=null)live.setText((Status.captureActive?"قراءة الشاشة مفعّلة":"القراءة غير مفعّلة")+" · الشاشات المتصلة: "+Status.clients+"\nصور مقروءة: "+Status.framesRead+"\n"+Status.message+"\n"+Status.metrics+"\nآخر إنجليزي: "+Status.observedOcr+"\nآخر عربي: "+Status.arabic);ui.postDelayed(this,900);}};
 @Override public void onCreate(Bundle saved){super.onCreate(saved);if(saved!=null){step=saved.getInt("step");onTv=saved.getBoolean("tv",true);}getWindow().setStatusBarColor(Ui.BG);getWindow().setNavigationBarColor(Ui.BG);render();}
 private void render(){
  LinearLayout root=Ui.column(this);root.setPadding(Ui.dp(this,16),Ui.dp(this,16),Ui.dp(this,16),Ui.dp(this,20));root.setBackgroundColor(Ui.BG);
  root.addView(Ui.text(this,"PS5 · "+(step==0?"طريقة التشغيل":"الخطوة "+(onTv?step+1:step==2?2:step==4?3:4)+" من "+(onTv?6:4)),25));page=Ui.card(this,root,new String[]{"طريقة اللعب","الثانوي على الكونسول أولًا","الثانوي نفسه في الجوال","الرجوع للأساسي من يد PS5","بدء الترجمة","حالة القراءة والترجمة"}[step]);live=null;
  if(step==0){
   page.addView(Ui.text(this,"اختر كيف تلعب. اقتران PS5 يتم في PS Remote Play الرسمي؛ تطبيقنا يقرأ صورته بإذن مشاركة Android.",15));
   Ui.button(this,page,"ألعب على التلفزيون وبيد PS5",true,()->{onTv=true;step=1;render();});
   Ui.button(this,page,"ألعب وأتحكم من الجوال",false,()->{onTv=false;step=2;render();});
   Ui.button(this,page,"البث مربوط مسبقًا — ابدأ الترجمة",false,()->{step=4;render();});
  }else if(step==1){
   page.addView(Ui.text(this,"1. في PS5 اضغط PS في اليد ← صورة المستخدم ← تبديل المستخدم.\n2. ادخل بحساب PSN ثانوي مجاني لا تلعب به. إذا لم يوجد: إضافة مستخدم، ثم سجّل دخوله مرة واحدة. لا تحتاج شراء اللعبة عليه.\n3. وأنت داخل الثانوي: الإعدادات ← النظام ← اللعب عن بُعد ← تمكين اللعب عن بُعد.\nخلك على الثانوي حتى يكتمل الاقتران بالجوال؛ نرجع للأساسي بعد ظهور الفيديو.",15));
   Ui.button(this,page,"جهزت الحساب الثانوي على PS5 — التالي",true,()->{step=2;render();});
  }else if(step==2){
   page.addView(Ui.text(this,onTv?"افتح PS Remote Play الرسمي بالجوال وسجّل بالحساب الثانوي نفسه الموجود على PS5، وليس حساب لعبك الأساسي. إذا كان تطبيق Sony مسجّلًا بالأساسي فسجّل خروجه ثم ادخل بالثانوي. اختر PS5 وانتظر ظهور الفيديو.\nإذا طلب Sony رقم الاقتران: على PS5 وأنت بالثانوي افتح الإعدادات ← النظام ← اللعب عن بُعد ← ربط جهاز، واكتب الرقم في تطبيق Sony فقط.":"افتح تطبيق Sony وسجّل بالحساب الذي ستلعب به، ثم اتصل بـPS5. الحساب الثانوي ليس مطلوبًا لمجرد اللعب والتحكم من الجوال؛ نستخدمه لفصل بث القراءة عن يد اللعب على التلفزيون.",15));
   Ui.button(this,page,"فتح / تثبيت PS Remote Play",true,this::openSony);
   Ui.button(this,page,"ظهر فيديو اللعبة — التالي",false,()->{step=onTv?3:4;render();});
  }else if(step==3){
   page.addView(Ui.text(this,"الآن بعد ما ظهر الفيديو بالجوال، واليد متصلة بـPS5 مباشرة وليست بالجوال:\n1. اضغط زر PS في يد التحكم المتصلة بالكونسول.\n2. اختر مستخدمك الأساسي الذي عليه لعبتك.\n3. افتح اللعبة وفعّل Subtitles الإنجليزية.\nلا تبدّل تسجيل الجوال إلى الأساسي؛ يظل الثانوي للبث فقط. إذا انقطع البث عند تبديل المستخدم، أعد اتصال الثانوي في Sony أولًا وتأكد من اختيار الأساسي باليد.",15));
   Ui.button(this,page,"رجعت للأساسي واليد تعمل — التالي",true,()->{step=4;render();});
  }else if(step==4){
   page.addView(Ui.text(this,"المحرك: "+AiPreferences.label(AiPreferences.provider(this))+"\n1. اربط الشاشة من الصفحة الرئيسية بالتلقائي ووافق على الرمز.\n2. فعّل Subtitles الإنجليزية في اللعبة.\n3. اضغط بدء ترجمة PS5، ثم وافق على إذن Android. إن عرض النظام اختيار تطبيق، اختر PS Remote Play. سيعيدك التطبيق إلى فيديو اللعبة تلقائيًا.",15));
   page.addView(Ui.text(this,"لا نكتشف حساب Sony أو الفيديو من التطبيق الرسمي دون إذن قراءة. إذا منع Sony الالتقاط وظهرت صورة سوداء فلن يعمل هذا المسار؛ استخدم PS Remote Play على اللابتوب بدلًا منه.",13));
   Ui.button(this,page,"إعداد محرك الترجمة",false,()->startActivity(new Intent(this,AiSettingsActivity.class)));
   Ui.button(this,page,"ربط الشاشة بالتلقائي",false,()->startActivity(new Intent(this,MainActivity.class).addFlags(Intent.FLAG_ACTIVITY_REORDER_TO_FRONT)));
   Ui.button(this,page,"بدء ترجمة PS5",true,this::requestCapture);
  }else{
   live=Ui.text(this,"",14);page.addView(live);
   page.addView(Ui.text(this,"تتوقف قراءة واجهة مترجمنا أثناء فتحها كي لا نترجم القوائم؛ ارجع لفيديو اللعبة لاستمرار القراءة. وصول صور لا يضمن أن فيديو Sony قابل للالتقاط. إذا لا يوجد إنجليزي أثناء حوار، أعد الإذن واختر تطبيق Sony أو جرّب الكمبيوتر.",13));
   Ui.button(this,page,"العودة إلى فيديو PS Remote Play",true,this::openSony);
   Ui.button(this,page,"بدء القراءة إذا توقفت",false,this::requestCapture);
   Ui.button(this,page,"إيقاف القراءة والربط",false,()->{stopService(new Intent(this,CaptureService.class));step=4;render();});
  }
  feedback=Ui.text(this,"",14);root.addView(feedback);
  Ui.button(this,root,"منطقة القراءة — الحوار / القوائم / رسائل الهاتف",false,()->ReadingOptions.show(this));
  if(step>0)Ui.button(this,root,"الخطوة السابقة",false,()->{step=step==4&&!onTv?2:step==2&&!onTv?0:step-1;render();});
  Ui.button(this,root,"الرئيسية",false,this::finish);ScrollView scroll=new ScrollView(this);scroll.setBackgroundColor(Ui.BG);scroll.addView(root);PhoneInsets.fit(scroll);setContentView(scroll);
 }
 private void openSony(){Intent launch=getPackageManager().getLaunchIntentForPackage("com.playstation.remoteplay");try{if(launch!=null)startActivity(launch);else startActivity(new Intent(Intent.ACTION_VIEW,Uri.parse("https://play.google.com/store/apps/details?id=com.playstation.remoteplay")));}catch(ActivityNotFoundException e){feedback.setText("ثبّت تطبيق PS Remote Play الرسمي ثم ارجع هنا.");}}
 private void requestCapture(){
  if(!AiPreferences.canTranslate(this)){feedback.setText("جهّز نموذج العربية من الرئيسية أو اختر محركًا جاهزًا أولًا.");return;}
  if(getPackageManager().getLaunchIntentForPackage("com.playstation.remoteplay")==null){feedback.setText("ثبّت PS Remote Play الرسمي وافتح فيديو اللعبة أولًا، ثم ارجع لبدء الترجمة.");return;}
  if(Status.captureActive){feedback.setText("القراءة مفعّلة بالفعل. ارجع للفيديو، أو أوقف القراءة أولًا لإعادة منح الإذن.");return;}
  try{MediaProjectionManager m=(MediaProjectionManager)getSystemService(MEDIA_PROJECTION_SERVICE);startActivityForResult(m.createScreenCaptureIntent(),701);}catch(Exception e){feedback.setText("تعذر طلب إذن قراءة الشاشة؛ جرّب الكمبيوتر.");}
 }
 @Override protected void onActivityResult(int request,int result,Intent data){super.onActivityResult(request,result,data);if(request!=701)return;if(result!=RESULT_OK||data==null){feedback.setText("لم يبدأ الالتقاط؛ إذن Android مطلوب لقراءة فيديو Sony.");return;}
  try{startForegroundService(new Intent(this,CaptureService.class).setAction("capture").putExtra("result",result).putExtra("data",data).putExtra("source","ps5"));step=5;render();openSony();}catch(Exception e){feedback.setText("تعذر بدء خدمة القراءة. أعد فتح التطبيق وامنح إذن Android مجددًا.");}
 }
 @Override protected void onSaveInstanceState(Bundle saved){saved.putInt("step",step);saved.putBoolean("tv",onTv);super.onSaveInstanceState(saved);}
 @Override protected void onResume(){super.onResume();ui.post(refresh);}
 @Override protected void onPause(){ui.removeCallbacks(refresh);super.onPause();}
 @Override protected void onDestroy(){ui.removeCallbacks(refresh);super.onDestroy();}
}
