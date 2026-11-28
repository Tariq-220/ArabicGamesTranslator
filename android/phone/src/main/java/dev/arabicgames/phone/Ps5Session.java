// SPDX-License-Identifier: AGPL-3.0-only
// Session lifecycle follows the Sefflex/BlackBox native Engine: no audio and no controller injection.
package dev.arabicgames.phone;
import android.content.Context;
import dev.arabicgames.phone.ps5.NativeBridge;
import java.nio.ByteBuffer;
import java.nio.charset.StandardCharsets;
import java.util.concurrent.*;
import java.util.concurrent.atomic.AtomicLong;

final class Ps5Session {
 private static final ScheduledExecutorService control=Executors.newSingleThreadScheduledExecutor(r->{Thread t=new Thread(r,"ps5-native-control");t.setDaemon(true);return t;});
 private static final AtomicLong epoch=new AtomicLong();private static volatile boolean wanted;private static volatile long handle;
 static volatile String state="متوقف";static volatile long frames;private static int retries;
 static synchronized void start(Context context){if(wanted)return;Context app=context.getApplicationContext();long generation=epoch.incrementAndGet();wanted=true;frames=0;state="جارٍ الاتصال المباشر بـPS5…";
  control.execute(()->{close();retries=0;connect(app,generation);});
 }
 private static void close(){long old=handle;handle=0;if(old!=0)NativeBridge.INSTANCE.sessionStop(old);}
 static void stop(){wanted=false;epoch.incrementAndGet();state="متوقف";control.execute(Ps5Session::close);}
 private static boolean current(long g){return wanted&&g==epoch.get();}
 private static void connect(Context app,long g){
  if(!current(g))return;
  try{
   if(!NativeBridge.available())throw new IllegalStateException();Ps5Store.State saved=Ps5Store.read(app);if(!saved.paired())throw new IllegalStateException();
   Boolean awake=Ps5Discovery.awake(saved.host);
   if(Boolean.FALSE.equals(awake)){
    state="جارٍ إيقاظ PS5 من وضع الراحة…";int end=0;while(end<saved.key.length&&saved.key[end]!=0)end++;long wake=Long.parseUnsignedLong(new String(saved.key,0,end,StandardCharsets.US_ASCII),16);NativeBridge.INSTANCE.wake(saved.host,wake);
    for(int n=0;n<15&&current(g)&&!Boolean.TRUE.equals(awake);n++)awake=Ps5Discovery.awake(saved.host);
   }
   if(!current(g))return;if(!Boolean.TRUE.equals(awake)){fail(g,"لم يصل رد من PS5. شغّله وتأكد من نفس الشبكة؛ استخدم إعادة البحث إذا تغير عنوانه.");return;}
   state="فتح بث PS5 المباشر — الحساب الثانوي للبث";
   handle=NativeBridge.INSTANCE.sessionStart(saved.host,saved.key,saved.morning,saved.account,5,new NativeBridge.SessionListener(){
    public void onLog(int level,String message){} // Native packet/key logs are deliberately not exported.
    public void onFrame(ByteBuffer buffer,int w,int h,int stride){if(!current(g))return;frames++;PhoneVideoPreview.publish(buffer,w,h,stride);CaptureService.ps5Rgba(buffer,w,h,stride);}
    public void onEvent(int event,int reason,String detail){
     if(!current(g))return;if(event==0){state="متصل مباشرة بـPS5 — ارجع لحسابك الأساسي من اليد وافتح اللعبة";return;}
     control.execute(()->{if(!current(g))return;close();
      if(event==2){fail(g,"مستخدم PS5 يطلب رمز دخول محليًا. افتح المستخدم الثانوي في الكونسول ثم أعد الاتصال.");return;}
      if(reason==2){fail(g,"PS5 رفض الربط المحفوظ. أعد الاقتران بنفس الثانوي ورمز الجهاز الجديد.");return;}
      if(reason==0){fail(g,"انتهت جلسة PS5.");return;}
      if(retries++>=3){fail(g,"تعذر استمرار الاتصال. أغلق Remote Play في الأجهزة الأخرى وتأكد من الشبكة ثم أعد البدء.");return;}
      state=reason==1?"جلسة Remote Play مستخدمة؛ أغلق تطبيق Sony الخارجي إن كان مفتوحًا.":"انقطع الاتصال — جارٍ إعادة الاتصال…";
      control.schedule(()->connect(app,g),3,TimeUnit.SECONDS);
     });
    }
   });
   if(handle==0)fail(g,"تعذر فتح محرك البث. أعد تشغيل PS5 وتأكد من إغلاق أي جلسة Remote Play أخرى.");
  }catch(Exception|LinkageError e){fail(g,"تعذر بدء PS5 المباشر أو قراءة الربط الآمن. راجع تسجيل Sony والاقتران.");}
 }
 private static void fail(long g,String message){if(!current(g))return;wanted=false;state=message;Status.message=message;CaptureService.ps5Failed();}
}
