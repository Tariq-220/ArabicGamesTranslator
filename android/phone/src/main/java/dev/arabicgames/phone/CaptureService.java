// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames.phone;
import android.app.*;
import android.content.*;
import android.content.pm.ServiceInfo;
import android.graphics.*;
import android.hardware.display.*;
import android.media.*;
import android.media.projection.*;
import android.os.*;
import android.util.DisplayMetrics;
import com.google.mlkit.vision.common.InputImage;
import com.google.mlkit.vision.text.*;
import com.google.mlkit.vision.text.latin.TextRecognizerOptions;
import com.google.mlkit.nl.translate.*;
import com.google.mlkit.common.model.DownloadConditions;
import dev.arabicgames.Pairing;
import dev.arabicgames.ArabicPack;
import dev.arabicgames.SubtitleGate;
import java.nio.ByteBuffer;
import java.util.concurrent.atomic.AtomicBoolean;

public final class CaptureService extends Service {
 static volatile CaptureService current;
 private volatile boolean directActive;
 private volatile ArabicPack pack=ArabicPack.empty();
 static void refreshPack(){CaptureService s=current;if(s!=null)s.loadPack();}
 private void loadPack(){try{java.io.File f=new java.io.File(getFilesDir(),"arabic-pack.json");if(f.exists())pack=PackFiles.parse(new String(java.nio.file.Files.readAllBytes(f.toPath()),java.nio.charset.StandardCharsets.UTF_8));}catch(Exception e){pack=ArabicPack.empty();}}
 private final Handler ui=new Handler(Looper.getMainLooper());
 private HandlerThread thread;private Handler frames;
 private MediaProjection projection;private VirtualDisplay display;private ImageReader reader;
 private TextRecognizer recognizer;private Translator translator;private RelayServer server;
 private PowerManager.WakeLock wake;
 private final AtomicBoolean busy=new AtomicBoolean();
 private final SubtitleGate gate=new SubtitleGate();
 private AiPreferences.Config engine;
 private final java.util.ArrayDeque<String> history=new java.util.ArrayDeque<>();
 private final java.util.concurrent.ExecutorService aiWorker=java.util.concurrent.Executors.newSingleThreadExecutor();
 private long cloudRetryAfter;
 private final dev.arabicgames.SubtitleLease lease=new dev.arabicgames.SubtitleLease();
 private volatile boolean closed,ready;private boolean modelDownloading;private long sampled,lastText,started;
 private int width,height,density;private volatile int epoch;
 private final Runnable testLoop=new Runnable(){public void run(){
  if(closed||server==null)return;
  if(projection==null&&!directActive)server.subtitle("آرثر: لدينا 25 دولارًا — اختبار الحروف العربية فوق HDMI",4500);
  Status.clients=server.clients();ui.postDelayed(this,5000);
 }};
 @Override public int onStartCommand(Intent intent,int flags,int id){
  if(intent==null){stopSelf();return START_NOT_STICKY;}
  if("stop".equals(intent.getAction())){stopSelf();return START_NOT_STICKY;}
  try {
   current=this;
   foreground(projection!=null||"capture".equals(intent.getAction()));
   if(server==null){String code=getSharedPreferences("relay",MODE_PRIVATE).getString("pairingCode",null);if(code==null){code=Pairing.createCode();getSharedPreferences("relay",MODE_PRIVATE).edit().putString("pairingCode",code).apply();}server=new RelayServer(code);server.start(15000,false);Status.code=code;ui.post(testLoop);}
   if("test".equals(intent.getAction())){server.subtitle("آرثر: هذا اختبار العربية — 25 دولارًا",5000);Status.message="اختبار الربط فقط؛ لترجمة اللعبة اختر Xbox مباشر بلا كاميرا. أدخل عنوان الجوال ورمزه في التلفزيون.";return START_NOT_STICKY;}
   if("direct".equals(intent.getAction())) {
    if(projection!=null){Status.message="أوقف مشاركة الشاشة أولًا قبل فتح وضع Xbox المباشر.";return START_NOT_STICKY;}
    epoch++;gate.absent();Status.framesRead=0;Status.observedOcr="";Status.source="";Status.arabic="";directActive=true;initEngine();Status.running=true;started=SystemClock.elapsedRealtime();lastText=started;return START_NOT_STICKY;
   }
   if(projection!=null)return START_NOT_STICKY;
   if(!"capture".equals(intent.getAction()))return START_NOT_STICKY;
   directActive=false;Status.captureActive=true;
   Intent data=Build.VERSION.SDK_INT>=33?intent.getParcelableExtra("data",Intent.class):intent.getParcelableExtra("data");
   projection=((MediaProjectionManager)getSystemService(MEDIA_PROJECTION_SERVICE)).getMediaProjection(intent.getIntExtra("result",Activity.RESULT_CANCELED),data);
   if(projection==null)throw new IllegalStateException("Projection not granted");
   initEngine();
   projection.registerCallback(new MediaProjection.Callback(){
    @Override public void onStop(){stopSelf();}
    @Override public void onCapturedContentResize(int w,int h){if(frames!=null)frames.post(()->resize(w,h));}
   },ui);
   DisplayMetrics metrics=new DisplayMetrics();getSystemService(android.view.WindowManager.class).getDefaultDisplay().getRealMetrics(metrics);density=metrics.densityDpi;
   frames.post(()->resize(metrics.widthPixels,metrics.heightPixels));
   wake=((PowerManager)getSystemService(POWER_SERVICE)).newWakeLock(PowerManager.PARTIAL_WAKE_LOCK,"ArabicGames:Capture");wake.acquire(4 * 60 * 60 * 1000L);
   Status.running=true;started=SystemClock.elapsedRealtime();lastText=started;
  }catch(Exception e){Status.message="تعذر بدء الخدمة: "+e.getClass().getSimpleName()+". أعد فتح التطبيق وامنح إذن المشاركة مجددًا.";stopSelf();}
  return START_NOT_STICKY;
 }
 private void foreground(boolean capturing){
   NotificationManager manager=getSystemService(NotificationManager.class);
   manager.createNotificationChannel(new NotificationChannel("capture","قراءة ترجمة اللعبة",NotificationManager.IMPORTANCE_LOW));
   PendingIntent stop=PendingIntent.getService(this,2,new Intent(this,CaptureService.class).setAction("stop"),PendingIntent.FLAG_IMMUTABLE|PendingIntent.FLAG_UPDATE_CURRENT);
   PendingIntent open=PendingIntent.getActivity(this,1,new Intent(this,MainActivity.class),PendingIntent.FLAG_IMMUTABLE|PendingIntent.FLAG_UPDATE_CURRENT);
   Notification notification=new Notification.Builder(this,"capture").setSmallIcon(android.R.drawable.ic_menu_view).setContentTitle("مترجم الألعاب يعمل").setContentText("الربط نشط؛ اضغط إيقاف لإنهائه.").setContentIntent(open).setOngoing(true).addAction(new Notification.Action.Builder(null,"إيقاف",stop).build()).build();
   if(Build.VERSION.SDK_INT>=29)startForeground(1,notification,ServiceInfo.FOREGROUND_SERVICE_TYPE_CONNECTED_DEVICE | (capturing?ServiceInfo.FOREGROUND_SERVICE_TYPE_MEDIA_PROJECTION:0));else startForeground(1,notification);
 }
 private void resize(int fullWidth,int fullHeight){
  if(closed||projection==null||fullWidth<1||fullHeight<1)return;
  float scale=Math.min(1f,1280f/Math.max(fullWidth,fullHeight));int w=Math.max(2,(int)(fullWidth*scale));int h=Math.max(2,(int)(fullHeight*scale));
  if(w==width&&h==height)return;width=w;height=h;epoch++;gate.absent();
  ImageReader next=ImageReader.newInstance(w,h,PixelFormat.RGBA_8888,2);
  try {
   if(display==null)display=projection.createVirtualDisplay("ArabicGameReader",w,h,density,DisplayManager.VIRTUAL_DISPLAY_FLAG_AUTO_MIRROR,next.getSurface(),null,frames);
   else {display.resize(w,h,density);display.setSurface(next.getSurface());}
   ImageReader previous=reader;reader=next;reader.setOnImageAvailableListener(this::capture,frames);if(previous!=null)previous.close();
  }catch(Exception e){next.close();Status.message="تعذر إعداد صورة الشاشة؛ أوقف القراءة وأعد منح الإذن.";ui.post(this::stopSelf);}
 }
 private void capture(ImageReader from){
  Image image=null;Bitmap cropped=null;
  long now=SystemClock.elapsedRealtime();
  try {
   image=from.acquireLatestImage();if(image==null||closed||!ready||now-sampled<300||!busy.compareAndSet(false,true))return;
   sampled=now;int generation=epoch;
   Image.Plane p=image.getPlanes()[0];ByteBuffer buffer=p.getBuffer();int paddedWidth=p.getRowStride()/p.getPixelStride();
   Bitmap full=Bitmap.createBitmap(paddedWidth,image.getHeight(),Bitmap.Config.ARGB_8888);full.copyPixelsFromBuffer(buffer);
   float[] region=ReadingRegion.bounds(getSharedPreferences("reading",0).getString("region","subtitles"));int y=(int)(image.getHeight()*region[1]);int left=(int)(image.getWidth()*region[0]);
   cropped=Bitmap.createBitmap(full,left,y,Math.max(1,(int)(image.getWidth()*region[2])),Math.max(1,(int)(image.getHeight()*region[3])));
   if(cropped!=full)full.recycle();
   Bitmap bitmap=cropped;cropped=null;
   processBitmap(bitmap,now,generation);
  }catch(Exception e){if(cropped!=null)cropped.recycle();busy.set(false);Status.message="تعذر التقاط إطار. تحقق من أن البث ظاهر والجوال في الوضع الأفقي.";}
  finally{if(image!=null)image.close();}
 }
 private void initEngine(){
  if(engine==null)loadTranslationSettings();
  if(thread==null){thread=new HandlerThread("latest-game-frame");thread.start();frames=new Handler(thread.getLooper());}
  if(recognizer!=null){ensureModel();return;}
  loadPack();
   recognizer=TextRecognition.getClient(TextRecognizerOptions.DEFAULT_OPTIONS);
   translator=Translation.getClient(new TranslatorOptions.Builder().setSourceLanguage(TranslateLanguage.ENGLISH).setTargetLanguage(TranslateLanguage.ARABIC).build());
   ensureModel();
 }
 static boolean screenTest(){CaptureService s=current;if(s==null||s.closed||s.server==null)return false;pauseDirect();String text="اختبار إرسال من الجوال إلى الشاشة — "+(SystemClock.elapsedRealtime()/1000%10000);Status.arabic=text;Status.message="اختبار إرسال لمدة 10 ثوانٍ. بعدها اضغط بدء الترجمة.";s.server.subtitle(text,10000);final int generation=s.epoch;s.ui.postDelayed(()->{if(!s.closed&&s.epoch==generation&&Status.arabic.equals(text)){Status.arabic="";s.server.clear();}},10000);return true;}
 static void retryModel(){CaptureService s=current;if(s!=null)s.ensureModel();}
 static void translationChanged(){CaptureService s=current;if(s==null||s.closed)return;
  s.epoch++;s.gate.resetMemory();s.lease.clear();s.history.clear();s.cloudRetryAfter=0;s.ready=false;
  Status.arabic="";Status.cloudMetrics="آخر طلب AI: لم يكتمل طلب بالمحرك الجديد بعد.";if(s.server!=null)s.server.clear();s.loadTranslationSettings();s.ensureModel();
 }
 private void loadTranslationSettings(){try{engine=AiPreferences.read(this);}catch(Exception e){engine=new AiPreferences.Config(AiPreferences.provider(this),"","","",AiPreferences.cloud(this));Status.message="تعذر قراءة مفتاح AI. افتح إعدادات المحرك وأعد إدخاله.";}}
 static String directState(){CaptureService s=current;if(s==null||s.closed)return "خدمة الربط متوقفة";if(!s.directActive)return "اختر وضع Xbox المباشر";if(!s.ready)return s.modelDownloading?"تجهيز نموذج العربية…":"نموذج العربية غير جاهز داخل جلسة القراءة";return s.busy.get()?"معالجة إطار":"جاهز لقراءة الفيديو";}
 private void ensureModel(){
  if(engine!=null&&engine.cloud()){ready=true;Status.message="محرك "+engine.label()+" جاهز للطلب؛ لم يُختبر الاتصال بعد.";return;}
  if(closed||translator==null||ready||modelDownloading)return;modelDownloading=true;Status.message="تجهيز نموذج العربية للجلسة…";
  translator.downloadModelIfNeeded(new DownloadConditions.Builder().requireWifi().build())
   .addOnSuccessListener(v->{if(closed)return;modelDownloading=false;ready=true;Status.modelReady=true;Status.message="النموذج جاهز للجلسة. شغّل قراءة الفيديو بعد ظهور اللعبة.";})
   .addOnFailureListener(e->{if(closed)return;modelDownloading=false;ready=false;Status.message="تعذر تجهيز النموذج للجلسة. افحص تنزيله من الصفحة الرئيسية ثم أعد فتح Xbox المباشر.";});
 }
 static void pauseDirect(){CaptureService s=current;if(s!=null&&s.directActive){s.epoch++;s.gate.absent();s.lease.clear();Status.arabic="";if(s.server!=null)s.server.clear();}}
 static boolean acceptsDirect(){CaptureService s=current;return s!=null&&!s.closed&&s.directActive&&s.ready&&!s.busy.get();}
 static void directJpeg(byte[] jpeg){directJpeg(jpeg,false,0);}
 static void directJpeg(byte[] jpeg,boolean cropFull,int rotation){
  CaptureService s=current;if(s==null||s.closed||!s.directActive||!s.ready||!s.busy.compareAndSet(false,true))return;
  int generation=s.epoch;long now=SystemClock.elapsedRealtime();
  s.frames.post(()->{
   Bitmap full=null,crop=null;
   try{
    if(s.closed||generation!=s.epoch){s.busy.set(false);return;}
    full=BitmapFactory.decodeByteArray(jpeg,0,jpeg.length);if(full==null)throw new IllegalArgumentException("Bad frame");
    if(rotation!=0){Matrix m=new Matrix();m.postRotate(rotation);Bitmap rotated=Bitmap.createBitmap(full,0,0,full.getWidth(),full.getHeight(),m,true);if(rotated!=full)full.recycle();full=rotated;}
    float[] region=ReadingRegion.bounds(s.getSharedPreferences("reading",0).getString("region","subtitles"));
    crop=cropFull?Bitmap.createBitmap(full,(int)(full.getWidth()*region[0]),(int)(full.getHeight()*region[1]),Math.max(1,(int)(full.getWidth()*region[2])),Math.max(1,(int)(full.getHeight()*region[3]))):full;
    if(crop!=full)full.recycle();full=null;Bitmap image=crop;crop=null;s.processBitmap(image,now,generation);
   }catch(Exception e){if(crop!=null)crop.recycle();if(full!=null)full.recycle();s.busy.set(false);Status.message="تعذرت قراءة إطار Xbox.";}
  });
 }
 private void processBitmap(Bitmap bitmap,long now,int generation){
   Status.framesRead++;
   recognizer.process(InputImage.fromBitmap(bitmap,0))
    .addOnSuccessListener(result->{
     bitmap.recycle();if(closed||generation!=epoch){busy.set(false);return;}
     String observed=ReadingRegion.sourceLimit(SubtitleGate.normalize(result.getText()));Status.observedOcr=observed;String text=DialogueProfile.english(observed,AiPreferences.game(this));long readDone=SystemClock.elapsedRealtime();
     if(text.isEmpty()){
      if(readDone-lastText>1200){gate.absent();lease.clear();if(!Status.arabic.isEmpty()){Status.arabic="";if(server!=null)server.clear();}}
      if(readDone-started>10000&&readDone-lastText>10000)Status.message="لم يظهر نص: فعّل ترجمة اللعبة الإنجليزية. قد يكون البث أسود أو محميًا من الالتقاط؛ لا يمكن قراءة محتوى محمي.";
      busy.set(false);return;
     }
     lastText=readDone;
     if(gate.alreadyShown(text)){if(lease.renew(text,readDone)){Status.arabic=lease.text();if(server!=null)server.subtitle(lease.text(),5000);}busy.set(false);return;}
     String packed=pack.find(text);
     if(packed!=null&&!gate.alreadyShown(text)){Status.source=text;publish(text,packed,now,readDone,readDone,generation,"حزمة عربية: "+pack.game);return;}
     if(!gate.accept(text,readDone)){busy.set(false);return;}
     Status.source=text;String known=gate.cached(text);
     if(known!=null){publish(text,known,now,readDone,readDone,generation,"ذاكرة الترجمة — "+(engine!=null&&engine.cloud()?engine.label():"محلي"));return;}
     if(engine!=null&&engine.cloud()){
      if(readDone<cloudRetryAfter){busy.set(false);return;}
      AiPreferences.Config selected=engine;java.util.List<String> context=new java.util.ArrayList<>(history);
      aiWorker.execute(()->{
       long sent=SystemClock.elapsedRealtime();
       try{String arabic=AiClient.translate(selected,text,context);
        ui.post(()->{
         long finished=SystemClock.elapsedRealtime();
         if(!closed&&generation==epoch)Status.cloudMetrics="آخر طلب AI: "+selected.label()+" | استجابة الطلب: "+(finished-sent)+" ms | قراءة إطاره: "+(readDone-now)+" ms";
         if(!closed&&generation==epoch&&finished-now>4500){gate.remember(text,arabic);busy.set(false);Status.message="تأخرت ترجمة AI؛ حُفظت للسطر المتكرر دون عرض سطر قديم.";return;}
         publish(text,arabic,now,readDone,finished,generation,selected.label());
        });
       }catch(AiClient.Failure error){ui.post(()->{if(!closed&&generation==epoch){gate.retry();cloudRetryAfter=SystemClock.elapsedRealtime()+15000;Status.message=error.getMessage()+" اختر المحلي من إعدادات المحرك إذا لزم.";}busy.set(false);});}
      });return;
     }
     translator.translate(text).addOnSuccessListener(arabic->publish(text,arabic,now,readDone,SystemClock.elapsedRealtime(),generation,"النموذج المحلي"))
      .addOnFailureListener(e->{gate.retry();Status.message="تعذرت ترجمة هذا السطر محليًا؛ ستُعاد المحاولة.";busy.set(false);});
    }).addOnFailureListener(e->{bitmap.recycle();Status.message="تعذرت قراءة الإطار.";busy.set(false);});
 }
 private void publish(String text,String arabic,long captured,long readDone,long translated,int generation,String origin){
  arabic=DialogueProfile.arabic(text,arabic,AiPreferences.game(this));
  try{
   if(closed||generation!=epoch)return;
   gate.remember(text,arabic);gate.shown(text);lease.store(text,arabic,translated);Status.arabic=arabic;
   if(getSharedPreferences("reading",0).getBoolean("history",true))DialogueStore.get(this).add(engine==null?"":engine.game,getSharedPreferences("reading",0).getString("region","subtitles"),text,arabic,origin);
   String context=text+" → "+arabic;if(history.isEmpty()||!history.peekLast().equals(context)){history.addLast(context);while(history.size()>3)history.removeFirst();}
   Status.metrics="المصدر: "+origin+" | قراءة: "+(readDone-captured)+" ms | ترجمة: "+(translated-readDone)+" ms | تجهيز السطر: "+(translated-captured)+" ms (لا يشمل بث Xbox أو عرض TV)";
   Status.message="ترجمة نشطة — "+origin;if(server!=null){server.subtitle(arabic,5000);Status.clients=server.clients();}
  }finally{busy.set(false);}
 }
 @Override public void onDestroy(){current=null;directActive=false;closed=true;epoch++;ready=false;Status.running=false;Status.captureActive=false;Status.clients=0;Status.code="";ui.removeCallbacks(testLoop);
  if(server!=null){server.clear();server.stop();server=null;}
  if(display!=null)display.release();if(reader!=null)reader.close();if(projection!=null)projection.stop();
  aiWorker.shutdownNow();if(thread!=null)thread.quitSafely();if(recognizer!=null)recognizer.close();if(translator!=null)translator.close();
  if(wake!=null&&wake.isHeld())wake.release();stopForeground(true);super.onDestroy();
 }
 @Override public IBinder onBind(Intent intent){return null;}
}
