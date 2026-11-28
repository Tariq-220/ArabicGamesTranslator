// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames.phone;
import android.graphics.Bitmap;
import android.os.SystemClock;
import java.nio.ByteBuffer;
/** Native preview is independent of OCR/model availability and has a bounded frame rate. */
final class PhoneVideoPreview {
 static volatile boolean enabled;static volatile Bitmap latest;private static long at;
 static synchronized void publish(ByteBuffer buffer,int w,int h,int stride){
  long now=SystemClock.elapsedRealtime();if((!enabled&&!ReadingCardsActivity.previewActive)||!Status.translatorUiVisible||now-at<100||w<2||h<2||w>3840||h>2160||stride<w*4||stride%4!=0||(long)stride*h>buffer.capacity())return;at=now;
  Bitmap full=null;
  try{full=Bitmap.createBitmap(stride/4,h,Bitmap.Config.ARGB_8888);ByteBuffer input=buffer.duplicate();input.rewind();full.copyPixelsFromBuffer(input);Bitmap visible=w==full.getWidth()?full:Bitmap.createBitmap(full,0,0,w,h);int target=Math.min(1280,w);Bitmap scaled=Bitmap.createScaledBitmap(visible,target,Math.max(1,h*target/w),true);if(visible!=full&&visible!=scaled)visible.recycle();if(full!=scaled)full.recycle();full=null;latest=scaled;}
  catch(RuntimeException|OutOfMemoryError e){if(full!=null)full.recycle();}
 }
 private static final java.util.concurrent.atomic.AtomicBoolean decoding=new java.util.concurrent.atomic.AtomicBoolean();
 private static final java.util.concurrent.ExecutorService previewWorker=java.util.concurrent.Executors.newSingleThreadExecutor(r->{Thread t=new Thread(r,"video-preview-only");t.setDaemon(true);return t;});
 static void jpeg(byte[] bytes){if(!ReadingCardsActivity.previewActive||!decoding.compareAndSet(false,true))return;previewWorker.execute(()->{Bitmap bitmap=null;try{bitmap=android.graphics.BitmapFactory.decodeByteArray(bytes,0,bytes.length);if(bitmap!=null)publishBitmap(bitmap);}catch(RuntimeException|OutOfMemoryError ignored){}finally{if(bitmap!=null)bitmap.recycle();decoding.set(false);}});}
 static void publishBitmap(Bitmap bitmap){if(ReadingCardsActivity.previewActive){int w=Math.min(1280,bitmap.getWidth());Bitmap scaled=Bitmap.createScaledBitmap(bitmap,w,Math.max(1,bitmap.getHeight()*w/bitmap.getWidth()),true);latest=scaled==bitmap?bitmap.copy(Bitmap.Config.ARGB_8888,false):scaled;}}
 static void clear(){enabled=false;latest=null;}
}
