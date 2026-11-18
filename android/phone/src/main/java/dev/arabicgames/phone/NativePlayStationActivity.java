// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames.phone;
import android.app.*;
import android.content.*;
import android.os.*;
import android.text.InputType;
import android.widget.*;
import dev.arabicgames.Pairing;
import dev.arabicgames.phone.ps5.NativeBridge;
import java.util.*;
import java.util.concurrent.*;

/** Direct PS5 path: Sony account -> LAN discovery -> PS5 PIN -> native frames -> existing Arabic pipeline. */
public final class NativePlayStationActivity extends Activity {
 private int step;private String chosenHost="",chosenName="PS5";private TextView status,live;private LinearLayout list;private EditText pin;
 private Ps5Store.State saved;private boolean busy,closed;private final Handler ui=new Handler(Looper.getMainLooper());
 private final ExecutorService worker=Executors.newSingleThreadExecutor();
 private final Runnable refresh=new Runnable(){public void run(){if(live!=null)live.setText(Ps5Session.state+"\nإطارات PS5: "+Ps5Session.frames+" · صور OCR: "+Status.framesRead+" · شاشات متصلة: "+Status.clients+"\n"+Status.message+"\n"+Status.metrics+"\nEnglish: "+Status.observedOcr+"\nالعربية: "+Status.arabic);ui.postDelayed(this,900);}};
 @Override public void onCreate(Bundle state){super.onCreate(state);load();step=saved!=null&&saved.paired()?4:saved!=null&&saved.account!=null?2:0;if(state!=null){step=state.getInt("step",step);chosenHost=state.getString("host","");chosenName=state.getString("name","PS5");}getWindow().setStatusBarColor(Ui.BG);getWindow().setNavigationBarColor(Ui.BG);render();}
 private void load(){try{saved=Ps5Store.read(this);}catch(Exception e){saved=null;}}
 private void render(){
  LinearLayout root=Ui.column(this);root.setBackgroundColor(Ui.BG);root.setPadding(Ui.dp(this,16),Ui.dp(this,16),Ui.dp(this,16),Ui.dp(this,16));root.addView(Ui.text(this,"PS5 · اتصال مباشر",25));
  LinearLayout card=Ui.card(this,root,new String[]{"1 · الثانوي على PS5 أولًا","2 · دخول Sony بالثانوي نفسه","3 · اكتشاف PS5 على الشبكة","4 · رمز الاقتران من PS5","5 · تشغيل الترجمة مباشرة"}[step]);live=null;list=null;pin=null;
  if(!NativeBridge.available())card.addView(Ui.text(this,"محرك PS5 الأصلي غير متاح في هذا التثبيت. يلزم ملف الجوال الذي يحتوي محرك PS5؛ لن نبدأ مشاركة شاشة بدلًا منه.",14));
  if(step==0){card.addView(Ui.text(this,"أضف حساب PSN ثانويًا مجانيًا على PS5 وسجّل دخوله. وأنت بالثانوي: الإعدادات ← النظام ← اللعب عن بُعد ← تمكين اللعب عن بُعد.\nالثانوي للبث فقط؛ الأساسي عليه لعبتك وتستخدمه باليد بعد الاقتران. لا تحتاج تطبيق Sony الخارجي لهذا المسار.",16));
   Ui.button(this,card,"جهزت الثانوي على PS5 — التالي",true,()->{step=1;render();});
  }else if(step==1){card.addView(Ui.text(this,"سجّل في صفحة Sony بالحساب الثانوي نفسه المفتوح على الكونسول. لا تدخل حساب اللعب الأساسي. نحفظ معرّف الحساب مشفّرًا؛ كلمة المرور تبقى لدى Sony ولا نحفظ رموز الدخول.",16));
   Ui.button(this,card,"تسجيل Sony بالحساب الثانوي",true,()->startActivityForResult(new Intent(this,PsnLoginActivity.class),802));
  }else if(step==2){card.addView(Ui.text(this,"الجوال وPS5 على نفس الشبكة. خل PS5 مفتوحًا على الثانوي، ثم اختر الجهاز من النتائج. إذا اقترنت سابقًا فلا تحتاج تسجيل Sony كل مرة.",15));
   Ui.button(this,card,"اكتشاف PS5 تلقائيًا",true,this::discover);list=Ui.column(this);card.addView(list);
   LinearLayout manual=Ui.fold(this,card,"عنوان يدوي عند تعذر الاكتشاف ▾");EditText host=new EditText(this);host.setHint("192.168.1.20");host.setSingleLine();host.setTextDirection(android.view.View.TEXT_DIRECTION_LTR);manual.addView(host);Ui.button(this,manual,"استخدام هذا العنوان المحلي",false,()->{String h=host.getText().toString().trim();if(!Pairing.privateIpv4(h)){status.setText("العنوان يجب أن يكون IPv4 محليًا.");return;}chosenHost=h;chosenName="PS5";step=3;render();});
  }else if(step==3){card.addView(Ui.text(this,"الجهاز: "+chosenName+"\nعلى PS5 وأنت بالحساب الثانوي: الإعدادات ← النظام ← اللعب عن بُعد ← ربط جهاز. اكتب رمز الـ8 أرقام هنا واترك شاشة الرمز مفتوحة حتى يكتمل الربط.",15));
   pin=new EditText(this);pin.setHint("00000000");pin.setInputType(InputType.TYPE_CLASS_NUMBER|InputType.TYPE_NUMBER_VARIATION_PASSWORD);pin.setSingleLine();pin.setFilters(new android.text.InputFilter[]{new android.text.InputFilter.LengthFilter(8)});card.addView(pin);Ui.button(this,card,"اقتران PS5 وحفظ الربط",true,this::pair);
  }else{
   card.addView(Ui.text(this,"PS5 المحفوظ: "+(saved==null?"غير متاح":saved.name)+"\nارجع للأساسي من يد التحكم المتصلة بالـPS5 وافتح لعبتك، واترك الثانوي للبث. فعّل Subtitles الإنجليزية. أغلق جلسة Sony Remote Play الخارجية إذا كانت تعمل؛ اتصالنا هنا مباشر.",15));
   live=Ui.text(this,"",13);card.addView(live);
   Ui.button(this,card,"بدء ترجمة PS5 مباشرة",true,()->{
    load();if(!NativeBridge.available()||saved==null||!saved.paired()){status.setText("أكمل تسجيل Sony والاقتران بمحرك PS5 أولًا.");return;}if(!AiPreferences.canTranslate(this)){status.setText("جهّز نموذج العربية أو اختر محرك الترجمة من الإعدادات.");return;}if(Status.captureActive){status.setText("توجد مشاركة شاشة قديمة. اضغط إيقاف المصدر أولًا ثم ابدأ PS5 المباشر.");return;}
    try{startForegroundService(new Intent(this,CaptureService.class).setAction("ps5"));status.setText("بدأ طلب البث المباشر؛ لا نحتاج إذن مشاركة شاشة أو فتح تطبيق Sony.");}catch(Exception e){status.setText("تعذر تشغيل خدمة PS5. افتح التطبيق من جديد وأعد المحاولة.");}
   });
   Ui.button(this,card,"محرك الترجمة واللعبة",false,()->startActivity(new Intent(this,AiSettingsActivity.class)));
   Ui.button(this,card,"ربط الشاشة بالتلقائي",false,()->startActivity(new Intent(this,MainActivity.class).addFlags(Intent.FLAG_ACTIVITY_REORDER_TO_FRONT)));
   Ui.button(this,card,"إيقاف المصدر والترجمة",false,()->stopService(new Intent(this,CaptureService.class)));
   Ui.button(this,card,"إعادة البحث والاقتران",false,()->{stopService(new Intent(this,CaptureService.class));step=2;render();});
  }
  status=Ui.text(this,"",14);root.addView(status);if(saved==null)status.setText("تعذرت قراءة التخزين الآمن. مفاتيح الترجمة لم تتغير؛ إذا لم يسبق الاقتران أكمل الإعداد، وإلا راجع بيانات التطبيق قبل حذف الربط.");
  if(step>0&&step<4)Ui.button(this,root,"الخطوة السابقة",false,()->{step--;render();});
  Ui.button(this,root,"نسيان حساب PS5 وربطه",false,()->new AlertDialog.Builder(this).setMessage("حذف ربط PS5 المحلي فقط؟ ستحتاج دخول الثانوي ورمز الاقتران مجددًا. مفاتيح الترجمة محفوظة.").setPositiveButton("نسيان الربط",(d,w)->{stopService(new Intent(this,CaptureService.class));Ps5Store.clear(this);load();step=0;render();}).setNegativeButton("إلغاء",null).show());
  Ui.button(this,root,"الرئيسية",false,this::finish);ScrollView scroll=new ScrollView(this);scroll.setBackgroundColor(Ui.BG);scroll.addView(root);PhoneInsets.fit(scroll);setContentView(scroll);
 }
 private void discover(){if(busy)return;if(!NativeBridge.available()){status.setText("محرك PS5 غير موجود في هذا الملف.");return;}busy=true;status.setText("جارٍ اكتشاف PS5…");worker.execute(()->{try{List<Ps5Discovery.Console> devices=Ps5Discovery.scan(this);runOnUiThread(()->{busy=false;if(closed||step!=2)return;list.removeAllViews();status.setText(devices.isEmpty()?"لم نجد PS5. تأكد من الشبكة والميزات البعيدة أو استخدم العنوان المحلي.":"اختر جهازك؛ عنوانه يُملأ تلقائيًا.");for(Ps5Discovery.Console device:devices)Ui.button(this,list,device.toString(),false,()->{chosenHost=device.host;chosenName=device.name;step=3;render();});});}catch(Exception e){runOnUiThread(()->{busy=false;if(!closed)status.setText("تعذر اكتشاف PS5. تأكد من الشبكة المحلية والمحرك.");});}});}
 private void pair(){if(busy)return;String digits=pin.getText().toString();if(!digits.matches("[0-9]{8}")||!Pairing.privateIpv4(chosenHost)){status.setText("اختر PS5 ورمزًا صحيحًا من 8 أرقام.");return;}if(!NativeBridge.available()){status.setText("محرك PS5 غير متاح.");return;}busy=true;String host=chosenHost;pin.setText("");status.setText("جارٍ الاقتران — أبقِ شاشة الرمز مفتوحة على PS5…");worker.execute(()->{String result;
  try{Ps5Store.State account=Ps5Store.read(this);if(account.account==null)throw new IllegalStateException();byte[] registered=new byte[64];int[] reason={0};int code=NativeBridge.INSTANCE.register(host,account.account,Integer.parseInt(digits),registered,reason,(level,message)->{});
   if(code!=0){result=reason[0]==0x80108b02?"PS5 لا يتعرف على الحساب. سجّل نفس الثانوي المفتوح على الكونسول.":reason[0]==0x80108b09||reason[0]==0x80108b12?"الرمز غير صحيح أو انتهت صلاحيته. افتح رمزًا جديدًا.":"لم يكتمل الاقتران. شغّل PS5 وافتح ربط جهاز في الثانوي ثم أعد المحاولة.";}
   else{int end=0;while(end<32&&registered[end]!=0)end++;String name=new String(registered,0,end,java.nio.charset.StandardCharsets.UTF_8);Ps5Store.paired(this,account.revision,host,name.isBlank()?"PS5":name,registered);result=null;}
  }catch(Exception|LinkageError e){result="تعذر الاقتران أو حفظ الربط الآمن. لا تعاود إدخال مفتاح ترجمة؛ راجع حساب Sony ورمز PS5.";}
  String message=result;runOnUiThread(()->{busy=false;if(closed)return;if(message==null){load();step=4;render();status.setText("تم حفظ PS5. ارجع للأساسي من اليد ثم ابدأ الترجمة.");}else status.setText(message);});
 });}
 @Override protected void onActivityResult(int req,int result,Intent data){super.onActivityResult(req,result,data);if(req==802){if(result==RESULT_OK){load();step=2;render();discover();}else status.setText("لم يكتمل دخول Sony. يلزم الحساب الثانوي للاقتران المباشر.");}}
 @Override protected void onSaveInstanceState(Bundle b){b.putInt("step",step);b.putString("host",chosenHost);b.putString("name",chosenName);super.onSaveInstanceState(b);}
 @Override protected void onResume(){super.onResume();ui.post(refresh);}
 @Override protected void onPause(){ui.removeCallbacks(refresh);super.onPause();}
 @Override public void onDestroy(){closed=true;worker.shutdown();ui.removeCallbacks(refresh);super.onDestroy();}
}
