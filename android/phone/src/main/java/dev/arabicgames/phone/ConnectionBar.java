// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames.phone;
import android.content.*;import android.os.*;import android.widget.*;
/** Fixed outside page scrolling; reports connection separately from translation. */
final class ConnectionBar extends LinearLayout {
 private final TextView state;private final Handler ui=new Handler(Looper.getMainLooper());
 ConnectionBar(Context c){super(c);setOrientation(HORIZONTAL);setGravity(android.view.Gravity.CENTER_VERTICAL);setBackgroundColor(Ui.BG);setPadding(Ui.dp(c,10),Ui.dp(c,4),Ui.dp(c,10),Ui.dp(c,4));state=Ui.text(c,"",12);addView(state,new LinearLayout.LayoutParams(0,-2,1));
  android.widget.Button test=Ui.button(c,this,"اختبار العربية\nعلى الشاشة",false,()->{if(!CaptureService.screenTest())c.startForegroundService(new Intent(c,CaptureService.class).setAction("test"));android.widget.Toast.makeText(c,"اختبار الربط فقط؛ ابدأ الترجمة لقراءة اللعبة",android.widget.Toast.LENGTH_LONG).show();});test.setLayoutParams(new LinearLayout.LayoutParams(Ui.dp(c,140),-2));
 }
 private final Runnable refresh=new Runnable(){public void run(){Context c=getContext();String provider=AiPreferences.provider(c);String model=AiPreferences.NATIVE.equals(provider)?(LocalModels.installed(c,LocalModels.selected(c))?"محفوظ على الجهاز":"يحتاج تنزيلًا"):AiPreferences.cloud(c)?"سحابي — يُفحص عند الطلب":Status.modelReady?"جاهز":"لم يُجهّز";
  state.setText("المحرك: "+model+" | الشاشة: "+(Status.clients>0?"متصلة ("+Status.clients+")":"غير متصلة")+"\nالقراءة: "+(Status.captureActive?"نشطة":"متوقفة")+" · "+(Status.arabic.isEmpty()?"بانتظار نص عربي":"وصل نص عربي"));ui.postDelayed(this,700);}};
 @Override protected void onAttachedToWindow(){super.onAttachedToWindow();ui.post(refresh);}
 @Override protected void onDetachedFromWindow(){ui.removeCallbacks(refresh);super.onDetachedFromWindow();}
 static LinearLayout dock(Context c,android.view.View page){LinearLayout box=Ui.column(c);box.addView(new ConnectionBar(c));box.addView(page,new LinearLayout.LayoutParams(-1,0,1));return box;}
}
