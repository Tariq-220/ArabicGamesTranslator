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
 static volatile CaptureService current;private static volatile boolean apiTestActive;
 static void suspendForApiTest(boolean active){apiTestActive=active;CaptureService s=current;if(active&&s!=null){s.epoch++;s.localQueue.reset();s.gate.absent();}}
 private volatile boolean directActive;
 private volatile ArabicPack pack=ArabicPack.empty();
 static void refreshPack(){CaptureService s=current;if(s!=null)s.loadPack();}
 private void loadPack(){try{java.io.File f=new java.io.File(getFilesDir(),"arabic-pack.json");if(f.exists())pack=PackFiles.parse(new String(java.nio.file.Files.readAllBytes(f.toPath()),java.nio.charset.StandardCharsets.UTF_8));}catch(Exception e){pack=ArabicPack.empty();}}
 private final Handler ui=new Handler(Looper.getMainLooper());
 private HandlerThread thread;private Handler frames;
 private String directSource="xbox";
 private MediaProjection projection;private VirtualDisplay display;private ImageReader reader;
 private TextRecognizer recognizer;private Translator translator;private RelayServer server;
 private PowerManager.WakeLock wake;
 private final AtomicBoolean busy=new AtomicBoolean();
 private final DialogueWorkQueue localQueue=new DialogueWorkQueue();private volatile long lastLocalOcr;
 private boolean isLocal(){return engine!=null&&(AiPreferences.NATIVE.equals(engine.provider)||AiPreferences.LOCAL.equals(engine.provider));}
 private boolean localOcrAllowed(){return SystemClock.elapsedRealtime()-lastLocalOcr>=(isLocal()?LocalTiming.interval(this):200);}
 private final SubtitleGate gate=new SubtitleGate();
 private volatile AiPreferences.Config engine;
 private String primaryRegion="";private PhoneAreas areas;private final java.util.concurrent.ExecutorService areaWorker=java.util.concurrent.Executors.newSingleThreadExecutor();
 private volatile LocalModels.Session localSession;private int localLoadEpoch=-1,modelGeneration;
 private final java.util.ArrayDeque<String> history=new java.util.ArrayDeque<>();
 private final java.util.concurrent.ExecutorService aiWorker=java.util.concurrent.Executors.newSingleThreadExecutor();
 private long cloudRetryAfter;private int cloudFailures;
 private long lastPresentedAt;private String presentedSource="",presentedArabic="";
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
   if("test".equals(intent.getAction())){server.subtitle("آرثر: هذا اختبار العربية — 25 دولارًا",5000);Status.message="اختبار الربط فقط؛ لترجمة اللعبة اختر Xbox أو بدء ترجمة PS5. من الشاشة اختر اكتشاف الجوال ووافق على الرمز.";return START_NOT_STICKY;}
   if("ps5-preview".equals(intent.getAction())) {
    if(projection!=null){Status.message="أوقف مشاركة الشاشة قبل بدء PS5 المباشر.";return START_NOT_STICKY;}
    directSource="ps5";Status.running=true;Ps5Session.start(this);return START_NOT_STICKY;
   }
   if("ps5-pause".equals(intent.getAction())){directActive=false;epoch++;localQueue.reset();gate.absent();Status.source="";Status.arabic="";if(server!=null)server.clear();Status.message="توقفت الترجمة؛ فيديو PS5 مستمر.";return START_NOT_STICKY;}
   if("ps5".equals(intent.getAction())) {
    if(projection!=null){Status.message="أوقف مشاركة الشاشة أولًا قبل تشغيل PS5 المباشر.";return START_NOT_STICKY;}
    dev.arabicgames.update.UpdateActivity.setPlaySessionActive(true);directSource="ps5";epoch++;gate.absent();Status.framesRead=0;Status.observedOcr="";Status.source="";Status.arabic="";directActive=true;initEngine();Status.running=true;started=SystemClock.elapsedRealtime();lastText=started;
    if(wake==null){wake=((PowerManager)getSystemService(POWER_SERVICE)).newWakeLock(PowerManager.PARTIAL_WAKE_LOCK,"ArabicGames:PS5");wake.acquire(4*60*60*1000L);}Ps5Session.start(this);return START_NOT_STICKY;
   }
   if("direct".equals(intent.getAction())) {
    Ps5Session.stop();dev.arabicgames.update.UpdateActivity.setPlaySessionActive(true);directSource="xbox";
    if(projection!=null){Status.message="أوقف مشاركة الشاشة أولًا قبل فتح وضع Xbox المباشر.";return START_NOT_STICKY;}
    epoch++;gate.absent();Status.framesRead=0;Status.observedOcr="";Status.source="";Status.arabic="";directActive=true;initEngine();Status.running=true;started=SystemClock.elapsedRealtime();lastText=started;return START_NOT_STICKY;
   }
   if(projection!=null)return START_NOT_STICKY;
   if(!"capture".equals(intent.getAction()))return START_NOT_STICKY;
   dev.arabicgames.update.UpdateActivity.setPlaySessionActive(true);directActive=false;Status.captureActive=true;
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
   image=from.acquireLatestImage();if(image==null||closed||!ready||!localOcrAllowed()||now-sampled<(isLocal()?LocalTiming.interval(this):300)||!busy.compareAndSet(false,true))return;
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
  primaryRegion=java.util.Arrays.toString(ReadingCards.primary(this));
  if(engine==null)loadTranslationSettings();
  if(thread==null){thread=new HandlerThread("latest-game-frame");thread.start();frames=new Handler(thread.getLooper());}
  if(recognizer!=null){ensureModel();return;}
  loadPack();
   recognizer=TextRecognition.getClient(TextRecognizerOptions.DEFAULT_OPTIONS);
   areas=new PhoneAreas(this,this::translateArea,value->{Status.areaCaptions.put(value.optString("id"),value);if(server!=null)server.area(value);});
   translator=Translation.getClient(new TranslatorOptions.Builder().setSourceLanguage(TranslateLanguage.ENGLISH).setTargetLanguage(TranslateLanguage.ARABIC).build());
   ensureModel();
 }
 static boolean screenTest(){CaptureService s=current;if(s==null||s.closed||s.server==null)return false;String text="اختبار إرسال من الجوال إلى الشاشة — "+(SystemClock.elapsedRealtime()/1000%10000);Status.arabic=text;Status.message="أُرسل اختبار الربط؛ قراءة اللعبة إن كانت تعمل تستمر.";s.server.subtitle(text,10000);final int generation=s.epoch;s.ui.postDelayed(()->{if(!s.closed&&s.epoch==generation&&Status.arabic.equals(text)){Status.arabic="";s.server.clear();}},10000);return true;}
 static void retryModel(){CaptureService s=current;if(s!=null)s.ensureModel();}
 static void readingCardsChanged(){CaptureService s=current;if(s==null)return;String geometry=java.util.Arrays.toString(ReadingCards.primary(s));if(!geometry.equals(s.primaryRegion)){s.primaryRegion=geometry;readingChanged();}else if(s.areas!=null)s.areas.reload();}
 static void readingChanged(){CaptureService s=current;if(s==null)return;if(s.areas!=null)s.areas.reload();s.epoch++;s.localQueue.reset();s.gate.resetMemory();s.lease.clear();s.history.clear();Status.arabic="";if(s.server!=null)s.server.clear();}
 static void translationChanged(){CaptureService s=current;if(s==null||s.closed)return;
  s.epoch++;s.localQueue.reset();if(s.localSession!=null){s.localSession.close();s.localSession=null;}s.modelDownloading=false;s.modelGeneration++;s.localLoadEpoch=-1;s.gate.resetMemory();s.lease.clear();s.history.clear();s.cloudRetryAfter=0;s.cloudFailures=0;s.ready=false;
  if(s.areas!=null)s.areas.reload();Status.arabic="";Status.cloudMetrics="آخر طلب AI: لم يكتمل طلب بالمحرك الجديد بعد.";if(s.server!=null)s.server.clear();s.loadTranslationSettings();s.ensureModel();
 }
 private void loadTranslationSettings(){try{engine=AiPreferences.read(this);}catch(Exception e){engine=new AiPreferences.Config(AiPreferences.provider(this),"","","",AiPreferences.cloud(this));Status.message="تعذر قراءة مفتاح AI. افتح إعدادات المحرك وأعد إدخاله.";}}
 static String directState(){CaptureService s=current;if(s==null||s.closed)return "خدمة الربط متوقفة";if(!s.directActive)return "اختر وضع Xbox المباشر";if(!s.ready)return s.modelDownloading?"تجهيز نموذج العربية…":"نموذج العربية غير جاهز داخل جلسة القراءة";return s.localQueue.working()?"المحرك يترجم — قراءة الحوار مستمرة":s.busy.get()?"معالجة إطار":"جاهز لقراءة الفيديو";}
 private void ensureModel(){
  if(engine!=null&&AiPreferences.NATIVE.equals(engine.provider)){
   if(closed||ready||localLoadEpoch==modelGeneration)return;
   if(!LocalModels.installed(this,engine.model)){Status.message="نزّل حزمة "+engine.model+" من إدارة النماذج المحلية.";return;}
   final int loadingEpoch=modelGeneration;localLoadEpoch=loadingEpoch;modelDownloading=true;String model=engine.model;Status.message="تحميل "+model+" في ذاكرة الجوال…";
   aiWorker.execute(()->{try{LocalModels.Session loaded=new LocalModels.Session(this,model);ui.post(()->{if(closed||modelGeneration!=loadingEpoch){loaded.close();if(!closed){modelDownloading=false;localLoadEpoch=-1;ensureModel();}return;}localSession=loaded;modelDownloading=false;ready=true;Status.message=model+" جاهز محليًا على الجوال.";});}catch(Exception|LinkageError e){ui.post(()->{if(closed||modelGeneration!=loadingEpoch)return;modelDownloading=false;localLoadEpoch=-1;ready=false;Status.message="تعذر تحميل النموذج المحلي؛ افحص الحزمة ومحرك التشغيل من إدارة النماذج.";});}});return;
  }
  if(engine!=null&&engine.cloud()){ready=true;Status.message="محرك "+engine.label()+" جاهز للطلب؛ لم يُختبر الاتصال بعد.";return;}
  if(closed||translator==null||ready||modelDownloading)return;final int downloadingGeneration=modelGeneration;modelDownloading=true;Status.message="تجهيز نموذج العربية للجلسة…";
  translator.downloadModelIfNeeded(new DownloadConditions.Builder().requireWifi().build())
   .addOnSuccessListener(v->{if(closed||modelGeneration!=downloadingGeneration)return;modelDownloading=false;ready=true;Status.modelReady=true;Status.message="النموذج جاهز للجلسة. شغّل قراءة الفيديو بعد ظهور اللعبة.";})
   .addOnFailureListener(e->{if(closed||modelGeneration!=downloadingGeneration)return;modelDownloading=false;ready=false;Status.message="تعذر تجهيز النموذج للجلسة. افحص تنزيله من الصفحة الرئيسية ثم أعد فتح Xbox المباشر.";});
 }
 static void pauseDirect(){CaptureService s=current;if(s!=null&&s.directActive){s.epoch++;s.localQueue.reset();s.gate.absent();s.lease.clear();Status.arabic="";if(s.server!=null)s.server.clear();}}
 static boolean acceptsDirect(){CaptureService s=current;return !apiTestActive&&s!=null&&!s.closed&&s.directActive&&!"ps5".equals(s.directSource)&&s.ready&&s.localOcrAllowed()&&!s.busy.get();}
 static void ps5Failed(){CaptureService s=current;if(s!=null&&"ps5".equals(s.directSource))s.ui.post(s::stopSelf);}
 static void ps5Rgba(java.nio.ByteBuffer buffer,int w,int h,int stride){
  CaptureService s=current;if(apiTestActive||s==null||s.closed||!s.directActive||!"ps5".equals(s.directSource)||!s.ready||!s.localOcrAllowed()||w<2||h<2||w>3840||h>2160||stride<w*4||stride%4!=0||((long)stride*h)>buffer.capacity()||!s.busy.compareAndSet(false,true))return;
  int generation=s.epoch;long now=SystemClock.elapsedRealtime();Bitmap full=null,crop=null;
  try{full=Bitmap.createBitmap(stride/4,h,Bitmap.Config.ARGB_8888);buffer.rewind();full.copyPixelsFromBuffer(buffer);if(s.areas!=null)s.areas.offer(full);float[] region=ReadingCards.primary(s);
   crop=Bitmap.createBitmap(full,(int)(w*region[0]),(int)(h*region[1]),Math.max(1,(int)(w*region[2])),Math.max(1,(int)(h*region[3])));if(crop!=full)full.recycle();full=null;Bitmap image=crop;crop=null;
   s.frames.post(()->{if(s.closed||generation!=s.epoch){image.recycle();s.busy.set(false);return;}s.processBitmap(image,now,generation);});
  }catch(Exception e){if(crop!=null)crop.recycle();if(full!=null)full.recycle();s.busy.set(false);Status.message="تعذرت قراءة إطار PS5.";}
 }
 static void directJpeg(byte[] jpeg){directJpeg(jpeg,false,0);}
 static void directJpeg(byte[] jpeg,boolean cropFull,int rotation){
  CaptureService s=current;if(s==null||s.closed||!s.directActive||!s.ready||!s.localOcrAllowed()||!s.busy.compareAndSet(false,true))return;
  int generation=s.epoch;long now=SystemClock.elapsedRealtime();
  s.frames.post(()->{
   Bitmap full=null,crop=null;
   try{
    if(s.closed||generation!=s.epoch){s.busy.set(false);return;}
    full=BitmapFactory.decodeByteArray(jpeg,0,jpeg.length);if(full==null)throw new IllegalArgumentException("Bad frame");
    if(rotation!=0){Matrix m=new Matrix();m.postRotate(rotation);Bitmap rotated=Bitmap.createBitmap(full,0,0,full.getWidth(),full.getHeight(),m,true);if(rotated!=full)full.recycle();full=rotated;}
    if(cropFull){PhoneVideoPreview.publishBitmap(full);if(s.areas!=null)s.areas.offer(full);}float[] region=ReadingCards.primary(s);
    crop=cropFull?Bitmap.createBitmap(full,(int)(full.getWidth()*region[0]),(int)(full.getHeight()*region[1]),Math.max(1,(int)(full.getWidth()*region[2])),Math.max(1,(int)(full.getHeight()*region[3]))):full;
    if(crop!=full)full.recycle();full=null;Bitmap image=crop;crop=null;s.processBitmap(image,now,generation);
   }catch(Exception e){if(crop!=null)crop.recycle();if(full!=null)full.recycle();s.busy.set(false);Status.message="تعذرت قراءة إطار Xbox.";}
  });
 }
 private void processBitmap(Bitmap bitmap,long now,int generation){
   if(apiTestActive||projection!=null&&Status.translatorUiVisible){bitmap.recycle();busy.set(false);return;}
   lastLocalOcr=SystemClock.elapsedRealtime();Status.framesRead++;PipelineMetrics.ocr.incrementAndGet();
   recognizer.process(InputImage.fromBitmap(bitmap,0))
    .addOnSuccessListener(result->{
     int bitmapWidth=bitmap.getWidth(),bitmapHeight=bitmap.getHeight();bitmap.recycle();if(closed||generation!=epoch){busy.set(false);return;}
     String observed=ReadingRegion.sourceLimit(SubtitleGate.normalize(dialogueText(result,bitmapWidth,bitmapHeight)));Status.observedOcr=observed;String cleaned=getSharedPreferences("reading",0).getBoolean("dialogueOnly",true)?CaptionNoise.clean(this,observed,AiPreferences.game(this)):observed;String text=DialogueProfile.english(cleaned,AiPreferences.game(this));long readDone=SystemClock.elapsedRealtime();
     if(text.isEmpty()){PipelineMetrics.empty.incrementAndGet();if(!observed.isEmpty()&&cleaned.isEmpty())PipelineMetrics.filtered.incrementAndGet();
      if(readDone-lastText>1200&&readDone-lastPresentedAt>=700){gate.absent();lease.clear();if(!Status.arabic.isEmpty()){Status.arabic="";if(server!=null)server.clear();}}
      if(!observed.isEmpty()&&cleaned.isEmpty())Status.message="تم تجاهل قوائم Xbox أو أسماء الطاقم. خيار الحوار فقط من قائمة الجلسة.";else if(readDone-started>10000&&readDone-lastText>10000)Status.message="لم يظهر نص: فعّل ترجمة اللعبة الإنجليزية. قد يكون البث أسود أو محميًا من الالتقاط؛ لا يمكن قراءة محتوى محمي.";
      busy.set(false);return;
     }
     lastText=readDone;
     if(gate.alreadyShown(text)){if(lease.renew(text,readDone)){Status.arabic=lease.text();if(server!=null)server.subtitle(lease.text(),5000);}busy.set(false);return;}
     String reviewed=DialogueStore.get(this).reviewed(AiPreferences.game(this),text);
     if(reviewed==null)reviewed=FormalArabic.ready(text);
     if(reviewed==null)reviewed=CommunityCorrections.find(this,AiPreferences.game(this),text);
     if(reviewed!=null){Status.source=text;publishReady(text,reviewed,now,readDone,generation,"تصحيحك المعتمد — بدون طلب محرك");return;}
     String packed=GameNames.same(pack.game,AiPreferences.game(this))?pack.find(text):null;
     if(packed!=null&&!FormalArabic.needsRephrase(text,packed)&&!gate.alreadyShown(text)){Status.source=text;publishReady(text,packed,now,readDone,generation,"حزمة عربية: "+pack.game);return;}
     if(!gate.accept(text,readDone,isLocal()?LocalTiming.settle(this):180)){busy.set(false);return;}
     Status.source=text;String known=gate.cached(text);
     if(known!=null){publishReady(text,known,now,readDone,generation,"ذاكرة الترجمة — "+(engine!=null&&engine.cloud()?engine.label():"محلي"));return;}
     if(engine!=null&&(engine.cloud()||AiPreferences.NATIVE.equals(engine.provider)||AiPreferences.LOCAL.equals(engine.provider))){
      if(readDone>=cloudRetryAfter){if(localQueue.offer(text,generation,now,readDone))PipelineMetrics.queued.incrementAndGet();}busy.set(false);drainLocalQueue();return;
     }
     translator.translate(text).addOnSuccessListener(arabic->publish(text,arabic,now,readDone,SystemClock.elapsedRealtime(),generation,"النموذج المحلي"))
      .addOnFailureListener(e->{PipelineMetrics.failures.incrementAndGet();gate.retry();Status.message="تعذرت ترجمة هذا السطر محليًا؛ ستُعاد المحاولة.";busy.set(false);});
    }).addOnFailureListener(e->{PipelineMetrics.ocrFailures.incrementAndGet();bitmap.recycle();Status.message="تعذرت قراءة الإطار.";busy.set(false);});
 }
 private void publishReady(String source,String arabic,long captured,long readDone,int generation,String origin){if(localQueue.working()){if(localQueue.offer(source,generation,captured,readDone,arabic,origin))PipelineMetrics.queued.incrementAndGet();busy.set(false);drainLocalQueue();}else publish(source,arabic,captured,readDone,readDone,generation,origin);}
 private void drainLocalQueue(){
  if(closed)return;DialogueWorkQueue.Job job=localQueue.take();if(job==null)return;LocalModels.Session local=localSession;AiPreferences.Config selected=engine;java.util.List<String> context=new java.util.ArrayList<>(history);
  aiWorker.execute(()->{try{boolean nativeModel=AiPreferences.NATIVE.equals(selected.provider);if(job.ready==null&&nativeModel&&local==null||closed||job.generation!=epoch)return;long sent=SystemClock.elapsedRealtime();String arabic=job.ready!=null?job.ready:nativeModel?local.translate(job.source):selected.cloud()?AiClient.translate(selected,job.source,context):com.google.android.gms.tasks.Tasks.await(translator.translate(job.source),8,java.util.concurrent.TimeUnit.SECONDS);long finished=SystemClock.elapsedRealtime();ui.post(()->{
    if(!closed&&job.generation==epoch){gate.remember(job.source,arabic);cloudFailures=0;Status.cloudMetrics=selected.label()+": "+selected.model+" | استدلال: "+(finished-sent)+" ms | انتظار: "+(sent-job.readDone)+" ms";
     if(localQueue.current(job)){if(finished-job.captured<=LocalTiming.maxAge(this))publish(job.source,arabic,job.captured,job.readDone,finished,job.generation,job.origin==null?selected.label():job.origin,false,finished-sent,sent-job.readDone);else{PipelineMetrics.expired.incrementAndGet();gate.remember(job.source,arabic);Status.message="النموذج المحلي بطيء لهذا السطر؛ حُفظ دون عرض نص قديم. اضبط أقصى عمر للسطر من سرعة القراءة المحلية.";}}else PipelineMetrics.superseded.incrementAndGet();}
   });
  }catch(Exception e){PipelineMetrics.failures.incrementAndGet();ui.post(()->{if(!closed&&job.generation==epoch&&localQueue.current(job)){gate.retry();cloudRetryAfter=SystemClock.elapsedRealtime()+1000;Status.message="تعذرت ترجمة السطر محليًا؛ ستُعاد المحاولة دون تراكم طلبات.";}});}finally{ui.post(()->{localQueue.finish(job);drainLocalQueue();});}});
 }
 private String dialogueText(com.google.mlkit.vision.text.Text result,int width,int height){
  if(!DialogueProfile.rdr2(AiPreferences.game(this)))return areas==null?result.getText():areas.dialogueText(result,width,height,ReadingCards.primary(this));StringBuilder text=new StringBuilder();float[] region=ReadingRegion.bounds(getSharedPreferences("reading",0).getString("region","subtitles"));
  for(com.google.mlkit.vision.text.Text.TextBlock block:result.getTextBlocks())for(com.google.mlkit.vision.text.Text.Line line:block.getLines()){Rect box=line.getBoundingBox();String value=line.getText();float center=box==null?0:region[0]+region[2]*box.exactCenterX()/Math.max(1,width);if(DialogueProfile.interactionPrompt(value,center))continue;if(text.length()>0)text.append('\n');text.append(value);}return text.toString();
 }
 private java.util.concurrent.CompletableFuture<String> translateArea(String text){
  java.util.concurrent.CompletableFuture<String> result=new java.util.concurrent.CompletableFuture<>();
  String known=DialogueStore.get(this).reviewed(AiPreferences.game(this),text);if(known==null)known=FormalArabic.ready(text);if(known==null)known=CommunityCorrections.find(this,AiPreferences.game(this),text);if(known!=null){result.complete(known);return result;}
  AiPreferences.Config selected=engine;if(closed||!ready||selected==null){result.completeExceptionally(new IllegalStateException("Engine not ready"));return result;}
  if(!selected.cloud()&&!AiPreferences.NATIVE.equals(selected.provider)){
   translator.translate(text).addOnSuccessListener(result::complete).addOnFailureListener(result::completeExceptionally);return result;
  }
  java.util.concurrent.ExecutorService queue=selected.cloud()?areaWorker:aiWorker;
  queue.execute(()->{if(closed){result.completeExceptionally(new IllegalStateException("Closed"));return;}try{LocalModels.Session local=localSession;String arabic=AiPreferences.NATIVE.equals(selected.provider)?(local==null?"":local.translate(text)):AiClient.translate(selected,text,java.util.Collections.emptyList());if(arabic.trim().isEmpty())throw new java.io.IOException("Empty translation");result.complete(FormalArabic.normalize(arabic));}catch(Exception error){result.completeExceptionally(error);}});return result;
 }
 private void publish(String text,String arabic,long captured,long readDone,long translated,int generation,String origin){publish(text,arabic,captured,readDone,translated,generation,origin,true);}
 private void publish(String text,String arabic,long captured,long readDone,long translated,int generation,String origin,boolean releaseFrame){publish(text,arabic,captured,readDone,translated,generation,origin,releaseFrame,-1,0);}
 private void publish(String text,String arabic,long captured,long readDone,long translated,int generation,String origin,boolean releaseFrame,long inferenceMs,long queueMs){
  if(!origin.startsWith("تصحيحك المعتمد"))arabic=CommunityCorrections.improve(this,AiPreferences.game(this),text,DialogueProfile.arabic(text,arabic,AiPreferences.game(this)));
  arabic=FormalArabic.normalize(arabic);
  try{
   if(closed||generation!=epoch)return;
   String previous=presentedArabic;long previousAt=lastPresentedAt;boolean stack=!previous.isEmpty()&&translated-previousAt<700&&!dev.arabicgames.SubtitleGate.fingerprint(presentedSource).equals(dev.arabicgames.SubtitleGate.fingerprint(text));
   final String latest=arabic,display=stack?previous+"\n"+arabic:arabic;final long presentation=SystemClock.elapsedRealtime();lastPresentedAt=presentation;presentedSource=text;presentedArabic=arabic;
   PipelineMetrics.published.incrementAndGet();gate.remember(text,arabic);gate.shown(text);lease.store(text,display,translated);Status.arabic=display;
   if(getSharedPreferences("reading",0).getBoolean("history",true))DialogueStore.get(this).add(engine==null?"":engine.game,getSharedPreferences("reading",0).getString("region","subtitles"),text,arabic,origin,readDone-captured,translated-readDone,translated-captured,inferenceMs,queueMs);
   String context=text+" → "+arabic;if(history.isEmpty()||!history.peekLast().equals(context)){history.addLast(context);while(history.size()>3)history.removeFirst();}
   Status.metrics="المصدر: "+origin+" | قراءة: "+(readDone-captured)+" ms | ترجمة: "+(translated-readDone)+" ms | تجهيز السطر: "+(translated-captured)+" ms (لا يشمل بث Xbox / PS5 أو عرض TV)";
   Status.message="ترجمة نشطة — "+origin;if(server!=null){server.subtitle(display,5000);Status.clients=server.clients();}
   if(stack)ui.postDelayed(()->{if(!closed&&generation==epoch&&lastPresentedAt==presentation&&Status.arabic.equals(display)){Status.arabic=latest;lease.store(text,latest,presentation);if(server!=null)server.subtitle(latest,2200);}},Math.max(100,700-(translated-previousAt)));
  }finally{if(releaseFrame)busy.set(false);}
 }
 @Override public void onDestroy(){dev.arabicgames.update.UpdateActivity.setPlaySessionActive(false);Ps5Session.stop();current=null;directActive=false;closed=true;if(areas!=null)areas.close();areaWorker.shutdownNow();Status.source="";Status.arabic="";localQueue.reset();epoch++;ready=false;Status.running=false;Status.captureActive=false;Status.clients=0;Status.code="";ui.removeCallbacks(testLoop);
  if(server!=null){server.clear();server.stop();server=null;}
  if(display!=null)display.release();if(reader!=null)reader.close();if(projection!=null)projection.stop();
  if(localSession!=null){localSession.close();localSession=null;}aiWorker.shutdownNow();if(thread!=null)thread.quitSafely();if(recognizer!=null)recognizer.close();if(translator!=null)translator.close();
  if(wake!=null&&wake.isHeld())wake.release();stopForeground(true);super.onDestroy();
 }
 @Override public IBinder onBind(Intent intent){return null;}
}
