// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames.phone;
import android.app.*;
import android.content.*;
import android.media.projection.*;
import android.net.Uri;
import android.os.*;
import android.view.*;
import android.widget.*;
import com.google.mlkit.nl.translate.*;
import com.google.mlkit.common.model.DownloadConditions;

public final class MainActivity extends Activity {
 private final Handler handler=new Handler(Looper.getMainLooper());
 private TextView status,summary,modelLabel;private Button download,start;private boolean modelReady;
 private Translator downloader;private String promptedPair;private AlertDialog pairingDialog;
 private void checkPairing(){RelayServer relay=RelayServer.active;if(relay==null)return;String challenge=relay.pendingChallenge();if(challenge==null||challenge.equals(promptedPair)||pairingDialog!=null)return;promptedPair=challenge;pairingDialog=new AlertDialog.Builder(this).setTitle("ربط الشاشة بدون كتابة").setMessage("تأكد أن الرمز نفسه ظاهر على شاشتك: "+challenge+"\nوافق فقط إذا أنت طلبت الربط الآن من الشاشة.").setPositiveButton("ربط شاشتي",(d,w)->relay.approve(challenge,true)).setNegativeButton("رفض",(d,w)->relay.approve(challenge,false)).create();pairingDialog.setOnDismissListener(d->pairingDialog=null);pairingDialog.show();}
 private final Runnable refresh=new Runnable(){public void run(){
  checkPairing();modelReady=AiPreferences.NATIVE.equals(AiPreferences.provider(MainActivity.this))?AiPreferences.canTranslate(MainActivity.this):modelReady||Status.modelReady;
  modelLabel.setText(AiPreferences.cloud(MainActivity.this)?"المحرك المختار: "+AiPreferences.label(AiPreferences.provider(MainActivity.this))+" — لا يلزم النموذج المحلي":modelReady?"✓ نموذج العربية جاهز":"افحص النموذج المحلي أو اختر AI");modelLabel.append("\nاللعبة: "+AiPreferences.game(MainActivity.this));modelLabel.setTextColor(modelReady?Ui.ACCENT:Ui.MUTED);
  summary.setText("الجوال: "+Status.address()+"\nرمز الربط: "+(Status.code.isEmpty()?"شغّل الربط أولًا":Status.code)+"\nشاشات متصلة: "+Status.clients);
  status.setText(Status.message+"\n"+Status.metrics+"\nEnglish: "+Status.observedOcr+"\nالعربية: "+Status.arabic);
  start.setEnabled((modelReady||AiPreferences.cloud(MainActivity.this))&&!Status.captureActive);handler.postDelayed(this,700);
 }};
 public void onCreate(Bundle state){super.onCreate(state);
  if(!AiPreferences.prefs(this).getBoolean("dialogueRepair413",false)){ReadingOptions.select(this,0);LocalTiming.save(this,150,80,4500);AiPreferences.prefs(this).edit().putBoolean("dialogueRepair413",true).apply();}
  getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);getWindow().setNavigationBarColor(Ui.BG);getWindow().setStatusBarColor(Ui.BG);
  LinearLayout root=Ui.column(this);root.setPadding(Ui.dp(this,16),Ui.dp(this,16),Ui.dp(this,16),Ui.dp(this,20));root.setBackgroundColor(Ui.BG);
  TextView title=Ui.text(this,"مترجم الألعاب",27);title.setTypeface(null,android.graphics.Typeface.BOLD);root.addView(title);
  root.addView(Ui.text(this,"Xbox · PS5 · كمبيوتر — الإصدار "+BuildConfig.VERSION_NAME,14));

  LinearLayout model=Ui.card(this,root,"1 · تجهيز العربية");modelLabel=Ui.text(this,"افحص نموذج الترجمة أولًا",15);model.addView(modelLabel);
  download=Ui.button(this,model,"المحلي فقط: فحص / تنزيل العربية",false,this::prepare);
  Ui.button(this,model,"محرك الترجمة: محلي / Gemini / OpenAI / DeepL",false,()->startActivity(new Intent(this,AiSettingsActivity.class)));
  LinearLayout play=Ui.card(this,root,"2 · قراءة اللعبة");
  Ui.button(this,play,"إصلاح توقف القراءة — استعادة الحوار",false,()->{ReadingOptions.select(this,0);LocalTiming.save(this,150,80,4500);CaptureService.readingChanged();Status.message="استُعيد الحوار وسرعة القراءة المعتدلة؛ النماذج والمفاتيح محفوظة. افتح جلسة اللعبة وابدأ الترجمة.";});play.addView(Ui.text(this,"افتح اللعبة، ثم اضغط بدء الترجمة. اترك التطبيق ظاهرًا والجوال على جنب.",14));
  Ui.button(this,play,"افتح Xbox — بدون كاميرا",true,()->startActivity(new Intent(this,XboxActivity.class)));
  Ui.button(this,play,"PS5 — ربط Sony وبدء الترجمة",true,()->startActivity(new Intent(this,NativePlayStationActivity.class).addFlags(Intent.FLAG_ACTIVITY_REORDER_TO_FRONT)));
  Ui.button(this,play,"PS5 / الكمبيوتر / OBS — طرق أخرى",false,()->startActivity(new Intent(this,SourcePathsActivity.class)));
  LinearLayout connection=Ui.card(this,root,"3 · ربط الشاشة — بدون كتابة");summary=Ui.text(this,"",16);summary.setTextIsSelectable(true);connection.addView(summary);
  Ui.button(this,connection,"تشغيل الربط / إرسال جملة اختبار",false,()->startForegroundService(new Intent(this,CaptureService.class).setAction("test")));
  Ui.button(this,connection,"فتح لوحة الترجمة في الشاشة المتصلة",false,()->{RelayServer relay=RelayServer.active;if(relay==null){Status.message="شغّل الربط أولًا واتصل بالشاشة.";return;}relay.openTvPanel();Status.message="أُرسل طلب فتح اللوحة إلى الشاشات المتصلة بالبث اللحظي.";});
  connection.addView(Ui.text(this,"شغّل الربط هنا، ثم اختر «اكتشاف الجوال أو الكمبيوتر» بالشاشة. وافق على الرمز المتطابق في الجوال. احفظ الربط مرة واحدة؛ العنوان والرمز اليدويان بديل عند تعذر الاكتشاف.",13));
  LinearLayout manual=Ui.fold(this,connection,"بديل الربط اليدوي ▾");
  Ui.button(this,manual,"نسخ عنوان الجوال ورمز الربط",false,()->{((android.content.ClipboardManager)getSystemService(CLIPBOARD_SERVICE)).setPrimaryClip(android.content.ClipData.newPlainText("ربط الشاشة",Status.address()+" / "+Status.code));android.widget.Toast.makeText(this,"تم النسخ",android.widget.Toast.LENGTH_SHORT).show();});
  Ui.button(this,connection,"شاشة ثانية / QR للمتصفح",false,()->startActivity(new Intent(this,SecondScreenActivity.class)));
  LinearLayout quality=Ui.card(this,root,"4 · جودة الترجمة والتصحيحات");
  quality.addView(Ui.text(this,"صحّح ترجمة خاطئة مرة واحدة؛ نستخدم تصحيحك تلقائيًا للنص نفسه في اللعبة نفسها. لا تُرفع التصحيحات ولا تُعتمد اقتراحات المحرك تلقائيًا.",13));
  Ui.button(this,quality,"السجل / تصحيح سطر / تصدير حزمة",false,()->startActivity(new Intent(this,HistoryActivity.class)));
  LinearLayout advanced=Ui.column(this);
  Ui.button(this,root,"مسارات أخرى وحزم جاهزة ▾",false,()->advanced.setVisibility(advanced.getVisibility()==View.VISIBLE?View.GONE:View.VISIBLE));root.addView(advanced);advanced.setVisibility(View.GONE);
  Ui.button(this,advanced,"ابدأ هنا — خطوات التفعيل",false,()->new android.app.AlertDialog.Builder(this).setTitle("تفعيل الترجمة").setMessage("1. فعّل الميزات البعيدة في Xbox، أو اللعب عن بُعد في PS5.\n2. افتح مصادر اللعبة واختر Xbox أو خطوات PS5. تسجيل الحساب يتم لدى Microsoft أو Sony.\n3. اختر المحرك: المحلي يحتاج تنزيلًا أول مرة؛ Gemini وOpenAI وDeepL تحتاج مفتاحك من زر الحصول على API وموافقة إرسال النص.\n4. اربط التلفزيون بالجوال بالرمز، أو استخدم الكمبيوتر: تطبيق التلفزيون الجديد يكتشف الكمبيوتر على الشبكة.\n5. فعّل النص الإنجليزي داخل اللعبة واختبر العربية ثم ابدأ الترجمة.\n6. للـMonitor: استخدم شاشة ثانية أو QR الكمبيوتر.\nQuickMT وجسر يعملان على الجوال بعد تنزيل الحزمة من صفحة النماذج، أو عبر خادم الكمبيوتر إن رغبت. حماية OLED داخل جلسة Xbox لا تعني أن قفل Android سيُبقي الالتقاط يعمل على كل الأجهزة.").setPositiveButton("فهمت",null).show());
  Ui.button(this,advanced,"استيراد حزمة عربية JSON",false,()->{Intent i=new Intent(Intent.ACTION_OPEN_DOCUMENT).setType("application/json").addCategory(Intent.CATEGORY_OPENABLE);try{startActivityForResult(i,703);}catch(ActivityNotFoundException e){Status.message="لا يوجد منتقي ملفات على الجهاز.";}});
  Ui.button(this,advanced,"كاميرا الجوال — اختيارية",false,()->startActivity(new Intent(this,CameraActivity.class)));
  Ui.button(this,advanced,"فتح Xbox في متصفح خارجي",false,()->{try{startActivity(new Intent(Intent.ACTION_VIEW,Uri.parse("https://www.xbox.com/remoteplay")));}catch(ActivityNotFoundException e){Status.message="ثبّت متصفحًا يدعم Xbox Remote Play.";}});
  start=Ui.button(this,advanced,"بديل: مشاركة شاشة Remote Play",false,()->{
   MediaProjectionManager m=(MediaProjectionManager)getSystemService(MEDIA_PROJECTION_SERVICE);
   Intent i=Build.VERSION.SDK_INT>=34?m.createScreenCaptureIntent(MediaProjectionConfig.createConfigForDefaultDisplay()):m.createScreenCaptureIntent();
   try{startActivityForResult(i,701);}catch(Exception e){Status.message="التقاط الشاشة غير متاح على هذا الجهاز.";}
  });start.setEnabled(false);
  status=Ui.text(this,"",13);advanced.addView(status);
  Ui.button(this,root,"التحديثات — فحص وتنزيل تلقائي",false,()->startActivity(new Intent(this,dev.arabicgames.update.UpdateActivity.class)));
  dev.arabicgames.update.UpdateActivity.onLaunch(this,"Phone");CommunityCorrections.refresh(this);
  Ui.button(this,root,"إيقاف القراءة والربط",false,()->{stopService(new Intent(this,CaptureService.class));Status.running=false;Status.message="متوقف";});
  ScrollView scroll=new ScrollView(this);scroll.setBackgroundColor(Ui.BG);scroll.addView(root);LinearLayout dock=ConnectionBar.dock(this,scroll);PhoneInsets.fit(dock);setContentView(dock);
  if(Build.VERSION.SDK_INT>=33&&checkSelfPermission(android.Manifest.permission.POST_NOTIFICATIONS)!=android.content.pm.PackageManager.PERMISSION_GRANTED)requestPermissions(new String[]{android.Manifest.permission.POST_NOTIFICATIONS},702);
 }
 private void prepare(){if(AiPreferences.NATIVE.equals(AiPreferences.provider(this))){startActivity(new Intent(this,LocalModelsActivity.class));return;}download.setEnabled(false);Status.message="جارٍ تنزيل نموذج الترجمة. اتصال Wi-Fi مطلوب أول مرة.";
  if(downloader==null)downloader=Translation.getClient(new TranslatorOptions.Builder().setSourceLanguage(TranslateLanguage.ENGLISH).setTargetLanguage(TranslateLanguage.ARABIC).build());
  downloader.downloadModelIfNeeded(new DownloadConditions.Builder().requireWifi().build())
   .addOnSuccessListener(v->{modelReady=true;Status.modelReady=true;CaptureService.retryModel();Status.message="النموذج جاهز. يمكنك الترجمة محليًا؛ الآن اختبر ربط التلفزيون.";download.setEnabled(true);})
   .addOnFailureListener(e->{Status.message="تعذر تنزيل النموذج. تحقق من Wi-Fi وخدمات Google ثم أعد المحاولة.";download.setEnabled(true);});
 }
 @Override protected void onActivityResult(int request,int result,Intent data){super.onActivityResult(request,result,data);
  if(request==703){
   if(result==RESULT_OK&&data!=null){try(java.io.InputStream in=getContentResolver().openInputStream(data.getData())){
    java.io.ByteArrayOutputStream out=new java.io.ByteArrayOutputStream();byte[] b=new byte[8192];int n;while((n=in.read(b))!=-1){if(out.size()+n>8*1024*1024)throw new java.io.IOException("Too large");out.write(b,0,n);}
    byte[] bytes=out.toByteArray();dev.arabicgames.ArabicPack pack=PackFiles.parse(new String(bytes,java.nio.charset.StandardCharsets.UTF_8));
    java.io.File temporary=new java.io.File(getFilesDir(),"arabic-pack.tmp"),destination=new java.io.File(getFilesDir(),"arabic-pack.json");
    try(java.io.FileOutputStream file=new java.io.FileOutputStream(temporary)){file.write(bytes);}
    java.nio.file.Files.move(temporary.toPath(),destination.toPath(),java.nio.file.StandardCopyOption.REPLACE_EXISTING);
    CaptureService.refreshPack();Status.message="تم استيراد "+pack.size()+" سطر عربي للعبة "+pack.game;
    if(!GameNames.same(pack.game,AiPreferences.game(this))&&!pack.game.isEmpty())new AlertDialog.Builder(this).setTitle("اسم اللعبة لا يطابق الحزمة").setMessage("الحزمة للعبة "+pack.game+" بينما الإعداد الحالي "+AiPreferences.game(this)+". لن نستخدم حزمة لعبة مع لعبة أخرى.").setPositiveButton("اختيار لعبة الحزمة",(dialog,which)->{AiPreferences.selectGame(this,pack.game);CaptureService.translationChanged();Status.message="تم اختيار "+pack.game+" وتفعيل حزمتها.";}).setNegativeButton("الإبقاء على اللعبة الحالية",null).show();
   }catch(Exception e){Status.message="تعذر استيراد الحزمة: يجب أن تكون JSON بتنسيق arabic-games-pack-v1 ومن الإنجليزية إلى العربية، وبحد أقصى 8 MB و20 ألف سطر.";}}
   return;
  }
  if(request!=701)return;if(result!=RESULT_OK||data==null){Status.message="لم يُمنح إذن مشاركة الشاشة.";return;}
  Intent i=new Intent(this,CaptureService.class).setAction("capture").putExtra("result",result).putExtra("data",data);
  startForegroundService(i);
  Status.message="بدأت المشاركة. انتقل إلى جلسة Remote Play؛ اترك الجوال مستيقظًا وصفحة اللعبة ظاهرة.";
 }
 @Override protected void onResume(){super.onResume();handler.post(refresh);}
 @Override protected void onPause(){handler.removeCallbacks(refresh);super.onPause();}
 @Override protected void onDestroy(){handler.removeCallbacks(refresh);if(downloader!=null)downloader.close();super.onDestroy();}
 private Button button(LinearLayout root,String text,Runnable action){Button b=new Button(this);b.setText(text);b.setOnClickListener(v->action.run());root.addView(b);return b;}
}
