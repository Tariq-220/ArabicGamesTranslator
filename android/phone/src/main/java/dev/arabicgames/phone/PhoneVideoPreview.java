// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames.phone;
import android.graphics.Bitmap;
import android.os.SystemClock;
import java.nio.ByteBuffer;
/** Native preview is independent of OCR/model availability and has a bounded frame rate. */
final class PhoneVideoPreview {
 static volatile boolean enabled;static volatile Bitmap latest;private static long at;
 static synchronized void publish(ByteBuffer buffer,int w,int h,int stride){
  long now=SystemClock.elapsedRealtime();if(!enabled||!Status.translatorUiVisible||now-at<100||w<2||h<2||w>3840||h>2160||stride<w*4||stride%4!=0||(long)stride*h>buffer.capacity())return;at=now;
  Bitmap full=null;
  try{full=Bitmap.createBitmap(stride/4,h,Bitmap.Config.ARGB_8888);ByteBuffer input=buffer.duplicate();input.rewind();full.copyPixelsFromBuffer(input);Bitmap visible=w==full.getWidth()?full:Bitmap.createBitmap(full,0,0,w,h);int target=Math.min(1280,w);Bitmap scaled=Bitmap.createScaledBitmap(visible,target,Math.max(1,h*target/w),true);if(visible!=full&&visible!=scaled)visible.recycle();if(full!=scaled)full.recycle();full=null;latest=scaled;}
  catch(RuntimeException|OutOfMemoryError e){if(full!=null)full.recycle();}
 }
 static void clear(){enabled=false;latest=null;}
}
