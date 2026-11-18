// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames.phone;
import android.app.*;
import android.content.*;
import android.os.*;
public final class LocalModelDownloadService extends Service {
 static volatile boolean running;static volatile String message="اختر نموذجًا لتنزيله مرة واحدة.";
 private final java.util.concurrent.ExecutorService worker=java.util.concurrent.Executors.newSingleThreadExecutor();
 private final Handler ui=new Handler(Looper.getMainLooper());
 @Override public void onCreate(){super.onCreate();((NotificationManager)getSystemService(NOTIFICATION_SERVICE)).createNotificationChannel(new NotificationChannel("local-models","تنزيل النماذج المحلية",NotificationManager.IMPORTANCE_LOW));}
 private Notification notification(){return new Notification.Builder(this,"local-models").setSmallIcon(android.R.drawable.stat_sys_download).setContentTitle("النموذج المحلي").setContentText(message).setOngoing(running).build();}
 @Override public int onStartCommand(Intent intent,int flags,int id){
  if(intent==null){stopSelf();return START_NOT_STICKY;}if(running)return START_NOT_STICKY;
  running=true;message="تجهيز التنزيل والتحقق…";startForeground(42,notification());String model=intent.getStringExtra("model");android.net.Uri imported=intent.getData();
  worker.execute(()->{try{LocalModels.install(this,model,imported,text->{message=text;((NotificationManager)getSystemService(NOTIFICATION_SERVICE)).notify(42,notification());});message="اكتمل التنزيل والتحقق. اختر النموذج من إعدادات المحرك ثم ابدأ الترجمة.";}catch(Exception e){message=e instanceof java.io.IOException?e.getMessage():"تعذر تجهيز النموذج. أعد المحاولة؛ الملفات المكتملة محفوظة.";}finally{running=false;ui.post(()->{CaptureService.retryModel();stopForeground(STOP_FOREGROUND_REMOVE);stopSelf();});}});return START_NOT_STICKY;
 }
 @Override public void onTimeout(int startId,int fgsType){worker.shutdownNow();running=false;message="توقف التنزيل بسبب مهلة النظام؛ أعد المحاولة للاستئناف.";stopSelf();}
 @Override public void onDestroy(){worker.shutdownNow();running=false;super.onDestroy();}
 @Override public IBinder onBind(Intent intent){return null;}
}
