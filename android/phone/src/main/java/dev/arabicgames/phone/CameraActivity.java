// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames.phone;
import android.Manifest;
import android.app.*;
import android.content.*;
import android.content.pm.PackageManager;
import android.graphics.*;
import android.hardware.Camera;
import android.os.*;
import android.view.*;
import android.widget.*;
import java.io.ByteArrayOutputStream;
import java.util.concurrent.*;
import java.util.concurrent.atomic.AtomicBoolean;

/** Optional camera path: console-independent, no Remote Play session or controller handover. */
@SuppressWarnings("deprecation")
public final class CameraActivity extends Activity implements SurfaceHolder.Callback {
 private Camera camera;private SurfaceView preview;private TextView status;
 private volatile boolean visible;private boolean surfaceReady;private long sampled;private int width,height,rotation;
 private final ExecutorService worker=Executors.newSingleThreadExecutor();private final AtomicBoolean processing=new AtomicBoolean();
 private final Handler ui=new Handler(Looper.getMainLooper());
 private final Runnable refresh=new Runnable(){public void run(){if(!visible)return;status.setText("ثبّت الجوال أفقيًا واملأ الإطار بشاشة اللعبة؛ فعّل الترجمة الإنجليزية. نقرأ الجزء السفلي من الصورة.\nIP: "+Status.address()+" | الرمز: "+Status.code+" | الشاشات: "+Status.clients+"\n"+Status.message+"\n"+Status.metrics);ui.postDelayed(this,700);}};
 @Override public void onCreate(Bundle state){super.onCreate(state);getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);
  LinearLayout root=new LinearLayout(this);root.setOrientation(LinearLayout.VERTICAL);root.setLayoutDirection(View.LAYOUT_DIRECTION_RTL);
  Button stop=new Button(this);stop.setText("إيقاف الكاميرا والعودة");stop.setOnClickListener(v->finish());root.addView(stop);
  status=new TextView(this);status.setTextSize(12);root.addView(status);
  preview=new SurfaceView(this);preview.getHolder().addCallback(this);root.addView(preview,new LinearLayout.LayoutParams(-1,0,1));PhoneInsets.fit(root);setContentView(root);
 }
 @Override protected void onResume(){super.onResume();visible=true;ui.post(refresh);
  if(checkSelfPermission(Manifest.permission.CAMERA)!=PackageManager.PERMISSION_GRANTED)requestPermissions(new String[]{Manifest.permission.CAMERA},801);else open();
 }
 @Override public void onRequestPermissionsResult(int request,String[] permissions,int[] results){super.onRequestPermissionsResult(request,permissions,results);if(request==801&&results.length>0&&results[0]==PackageManager.PERMISSION_GRANTED)open();else Status.message="لم يُمنح إذن الكاميرا.";}
 private void open(){if(!visible||!surfaceReady||camera!=null||checkSelfPermission(Manifest.permission.CAMERA)!=PackageManager.PERMISSION_GRANTED)return;
  try{
   int selected=-1;Camera.CameraInfo info=new Camera.CameraInfo();for(int i=0;i<Camera.getNumberOfCameras();i++){Camera.getCameraInfo(i,info);if(info.facing==Camera.CameraInfo.CAMERA_FACING_BACK){selected=i;break;}}
   if(selected<0){Status.message="لم تُعثر على كاميرا خلفية.";return;}
   camera=Camera.open(selected);Camera.getCameraInfo(selected,info);
   int display=getWindowManager().getDefaultDisplay().getRotation();int degrees=display==Surface.ROTATION_90?90:display==Surface.ROTATION_180?180:display==Surface.ROTATION_270?270:0;rotation=(info.orientation-degrees+360)%360;camera.setDisplayOrientation(rotation);
   Camera.Parameters parameters=camera.getParameters();Camera.Size chosen=null;
   for(Camera.Size size:parameters.getSupportedPreviewSizes())if(size.width>=960&&size.width<=1280&&(chosen==null||Math.abs(size.width/(double)size.height-16d/9)<Math.abs(chosen.width/(double)chosen.height-16d/9)))chosen=size;
   if(chosen!=null)parameters.setPreviewSize(chosen.width,chosen.height);
   parameters.setPreviewFormat(ImageFormat.NV21);if(parameters.getSupportedFocusModes().contains(Camera.Parameters.FOCUS_MODE_CONTINUOUS_VIDEO))parameters.setFocusMode(Camera.Parameters.FOCUS_MODE_CONTINUOUS_VIDEO);
   camera.setParameters(parameters);Camera.Size size=camera.getParameters().getPreviewSize();width=size.width;height=size.height;
   camera.setPreviewDisplay(preview.getHolder());
   camera.setPreviewCallbackWithBuffer((data,source)->{
    long now=SystemClock.elapsedRealtime();
    if(visible&&now-sampled>=350&&CaptureService.acceptsDirect()&&processing.compareAndSet(false,true)){
     sampled=now;byte[] copy=data.clone();final int w=width,h=height,rotate=rotation;
     worker.execute(()->{try{YuvImage image=new YuvImage(copy,ImageFormat.NV21,w,h,null);ByteArrayOutputStream out=new ByteArrayOutputStream();image.compressToJpeg(new Rect(0,0,w,h),75,out);if(visible)CaptureService.directJpeg(out.toByteArray(),true,rotate);}catch(Exception ignored){}finally{processing.set(false);}});
    }
    try{source.addCallbackBuffer(data);}catch(Exception ignored){}
   });
   int bytes=width*height*3/2;camera.addCallbackBuffer(new byte[bytes]);camera.addCallbackBuffer(new byte[bytes]);camera.startPreview();startForegroundService(new Intent(this,CaptureService.class).setAction("direct"));
  }catch(Exception e){release();Status.message="تعذر فتح الكاميرا. أغلق التطبيقات التي تستخدمها ثم جرّب مجددًا.";}
 }
 private void release(){if(camera!=null){camera.setPreviewCallbackWithBuffer(null);camera.stopPreview();camera.release();camera=null;}}
 @Override protected void onPause(){visible=false;ui.removeCallbacks(refresh);release();CaptureService.pauseDirect();super.onPause();}
 @Override public void onDestroy(){worker.shutdownNow();CaptureService.pauseDirect();super.onDestroy();}
 @Override public void onConfigurationChanged(android.content.res.Configuration configuration){super.onConfigurationChanged(configuration);release();CaptureService.pauseDirect();open();}
 @Override public void surfaceCreated(SurfaceHolder holder){surfaceReady=true;open();}
 @Override public void surfaceChanged(SurfaceHolder holder,int format,int w,int h){}
 @Override public void surfaceDestroyed(SurfaceHolder holder){surfaceReady=false;release();}
}
