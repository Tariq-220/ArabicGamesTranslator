// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames.phone;
import android.app.Activity;
import android.content.*;
import android.graphics.Bitmap;
import android.os.*;
import android.widget.*;
import com.google.zxing.*;
import com.google.zxing.common.BitMatrix;
import com.google.zxing.qrcode.QRCodeWriter;
public final class SecondScreenActivity extends Activity {
 private final Handler ui=new Handler(Looper.getMainLooper());private TextView link;private ImageView qr;private String current="";
 private final Runnable update=new Runnable(){public void run(){String url="http://"+Status.address()+":8787/screen?code="+Status.code;
  if(!Status.code.isEmpty()&&!url.equals(current)){current=url;link.setText(url);try{BitMatrix bits=new QRCodeWriter().encode(url,BarcodeFormat.QR_CODE,420,420);Bitmap bitmap=Bitmap.createBitmap(420,420,Bitmap.Config.RGB_565);for(int y=0;y<420;y++)for(int x=0;x<420;x++)bitmap.setPixel(x,y,bits.get(x,y)?0xff000000:0xffffffff);qr.setImageBitmap(bitmap);}catch(Exception e){link.setText("تعذر إنشاء QR؛ انسخ الرابط بعد تشغيل مصدر الترجمة.");}}
  ui.postDelayed(this,700);
 }};
 @Override public void onCreate(Bundle state){super.onCreate(state);LinearLayout root=Ui.column(this);root.setBackgroundColor(Ui.BG);root.setPadding(20,20,20,20);
  root.addView(Ui.text(this,"شاشة ثانية عبر المتصفح",24));root.addView(Ui.text(this,"شغّل الربط من الصفحة الرئيسية أولًا، ثم امسح QR بجهاز آخر على الشبكة نفسها. لا تشارك الرابط خارج البيت؛ يحتوي رمز الربط. بعد المسح ارجع لجلسة Xbox وابدأ القراءة. جهاز المصدر يبقى يعمل.",15));
  qr=new ImageView(this);root.addView(qr,new LinearLayout.LayoutParams(-1,Ui.dp(this,260)));link=Ui.text(this,"بانتظار تشغيل مصدر الترجمة…",15);link.setTextIsSelectable(true);link.setTextDirection(android.view.View.TEXT_DIRECTION_LTR);root.addView(link);
  Ui.button(this,root,"نسخ رابط الشاشة الثانية",false,()->{if(!current.isEmpty())((android.content.ClipboardManager)getSystemService(CLIPBOARD_SERVICE)).setPrimaryClip(ClipData.newPlainText("شاشة الترجمة",current));});Ui.button(this,root,"رجوع",false,this::finish);
  ScrollView scroll=new ScrollView(this);scroll.addView(root);PhoneInsets.fit(scroll);setContentView(scroll);
 }
 @Override protected void onResume(){super.onResume();ui.post(update);}
 @Override protected void onPause(){ui.removeCallbacks(update);super.onPause();}
}
