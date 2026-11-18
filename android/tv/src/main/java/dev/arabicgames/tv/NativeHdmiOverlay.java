// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames.tv;
import android.app.*;import android.content.*;import android.graphics.PixelFormat;import android.os.*;import android.provider.Settings;import android.view.*;import android.widget.*;import dev.arabicgames.Pairing;import org.json.*;import java.net.*;import java.io.*;import java.nio.charset.StandardCharsets;
/** Receives captions independently while the television's native HDMI application owns HDR. */
public final class NativeHdmiOverlay extends Service {
 static volatile boolean active;private volatile boolean closed;private volatile HttpURLConnection connection;private Thread worker;private WindowManager manager;private TextView caption;private final Handler ui=new Handler(Looper.getMainLooper());private long revision;private final Runnable hide=()->{if(caption!=null)caption.setVisibility(View.INVISIBLE);};
 static boolean validSource(String host,String code,boolean desktop){return Pairing.privateIpv4(host)&&(desktop||Pairing.validCode(code));}
 @Override public void onCreate(){super.onCreate();}
 @Override public int onStartCommand(Intent intent,int flags,int id){
  if(intent!=null&&"stop".equals(intent.getAction())){stopSelf();return START_NOT_STICKY;}
  if(active)return START_NOT_STICKY;
  var p=getSharedPreferences("MainActivity",0);String host=p.getString("host",""),code=p.getString("code",""),receiver=p.getString("receiverId","");boolean desktop=p.getBoolean("desktop",true);
  if(!Settings.canDrawOverlays(this)||!validSource(host,code,desktop)){stopSelf();return START_NOT_STICKY;}
  try{
   NotificationManager notifications=getSystemService(NotificationManager.class);notifications.createNotificationChannel(new NotificationChannel("native-hdmi","ترجمة فوق HDMI الأصلي",NotificationManager.IMPORTANCE_LOW));
   PendingIntent stop=PendingIntent.getService(this,10,new Intent(this,NativeHdmiOverlay.class).setAction("stop"),PendingIntent.FLAG_IMMUTABLE|PendingIntent.FLAG_UPDATE_CURRENT);
   PendingIntent open=PendingIntent.getActivity(this,11,new Intent(this,MainActivity.class),PendingIntent.FLAG_IMMUTABLE|PendingIntent.FLAG_UPDATE_CURRENT);
   startForeground(31,new Notification.Builder(this,"native-hdmi").setSmallIcon(android.R.drawable.ic_menu_view).setContentTitle("ترجمة فوق HDMI الأصلي — HDR").setContentText("افتح التطبيق لتعديل الربط أو إيقاف هذا الوضع").setContentIntent(open).addAction(new Notification.Action.Builder(null,"إيقاف",stop).build()).setOngoing(true).build());
   manager=getSystemService(WindowManager.class);caption=new TextView(this);caption.setTextColor(0xffffffff);caption.setTextSize(Math.max(14,Math.min(34,p.getInt("subtitleSize",20))));caption.setGravity(Gravity.CENTER);caption.setTextDirection(View.TEXT_DIRECTION_RTL);caption.setPadding(12,6,12,6);caption.setBackgroundColor(0xcc000000);caption.setVisibility(View.INVISIBLE);
   WindowManager.LayoutParams params=new WindowManager.LayoutParams(-2,-2,WindowManager.LayoutParams.TYPE_APPLICATION_OVERLAY,WindowManager.LayoutParams.FLAG_NOT_FOCUSABLE|WindowManager.LayoutParams.FLAG_NOT_TOUCHABLE,PixelFormat.TRANSLUCENT);params.gravity=Gravity.BOTTOM|Gravity.CENTER_HORIZONTAL;params.y=(int)(getResources().getDisplayMetrics().heightPixels*Math.max(2,Math.min(35,p.getInt("subtitleBottom",7)))/100f);caption.setMaxWidth((int)(getResources().getDisplayMetrics().widthPixels*.84));manager.addView(caption,params);active=true;
   final String url="http://"+host+":8787/events"+(desktop?"":"?code="+code+"&receiver="+java.net.URLEncoder.encode(receiver,StandardCharsets.UTF_8));worker=new Thread(()->receive(url),"native-hdmi-captions");worker.start();
  }catch(Exception e){Toast.makeText(this,"تعذر الظهور فوق HDMI الأصلي؛ راجع إذن الظهور فوق التطبيقات.",Toast.LENGTH_LONG).show();stopSelf();}
  return START_NOT_STICKY;
 }
 private void receive(String url){while(!closed){try{
  HttpURLConnection c=(HttpURLConnection)new URL(url).openConnection();connection=c;c.setConnectTimeout(4000);c.setReadTimeout(45000);c.setRequestProperty("Accept","text/event-stream");int status=c.getResponseCode();if(status==403){ui.post(()->{Toast.makeText(this,"راجع رمز ربط الشاشة في التطبيق.",Toast.LENGTH_LONG).show();stopSelf();});return;}if(status!=200)throw new IOException();
  try(BufferedReader reader=new BufferedReader(new InputStreamReader(c.getInputStream(),StandardCharsets.UTF_8))){String line,event="",data="";while(!closed&&(line=reader.readLine())!=null){if(line.length()>20000)throw new IOException();if(line.startsWith("event: "))event=line.substring(7);else if(line.startsWith("data: "))data=line.substring(6);else if(line.isEmpty()){if(event.equals("subtitle")||event.equals("clear")){final String kind=event;final JSONObject json=new JSONObject(data);ui.post(()->apply(kind,json));}event="";data="";}}}
 }catch(Exception ignored){}finally{if(connection!=null)connection.disconnect();}if(!closed)try{Thread.sleep(1000);}catch(InterruptedException e){return;}}}
 private void apply(String event,JSONObject json){if(closed||caption==null)return;long rev=json.optLong("revision",0);if(rev>0&&rev<revision)return;revision=Math.max(revision,rev);ui.removeCallbacks(hide);if(event.equals("clear")){caption.setVisibility(View.INVISIBLE);return;}String text=json.optString("text","");if(text.isEmpty())return;if(!text.contentEquals(caption.getText()))caption.setText(text);caption.setVisibility(View.VISIBLE);ui.postDelayed(hide,Math.max(250,Math.min(15000,json.optInt("duration",5000))));}
 @Override public void onDestroy(){closed=true;active=false;if(connection!=null)connection.disconnect();if(worker!=null)worker.interrupt();ui.removeCallbacksAndMessages(null);if(caption!=null&&manager!=null)try{manager.removeView(caption);}catch(Exception ignored){}super.onDestroy();}
 @Override public IBinder onBind(Intent intent){return null;}
}
