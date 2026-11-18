// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames.phone;
import android.app.*;
import android.content.*;
import android.os.Bundle;
import android.widget.*;
import org.json.*;

public final class HistoryActivity extends Activity {
 private LinearLayout rows;private TextView status;private String pendingExport;private boolean exportingLog;
 @Override public void onCreate(Bundle saved){super.onCreate(saved);
  LinearLayout root=Ui.column(this);root.setBackgroundColor(Ui.BG);root.setPadding(Ui.dp(this,16),Ui.dp(this,16),Ui.dp(this,16),Ui.dp(this,16));root.addView(Ui.text(this,"سجل الحوارات والترجمة",24));
  root.addView(Ui.text(this,"آخر 200 سطر محفوظة على هذا الجوال فقط. تصحيحك المعتمد يُستخدم تلقائيًا عند تكرار النص نفسه في اللعبة نفسها؛ لا نعتمد ناتج AI كتصحيح. التصحيحات المعتمدة محفوظة منفصلة حتى لو خرج السطر من آخر 200 سطر؛ المسح الصريح يحذفها أيضًا. السطور الآلية قد تكون خاطئة. لا يوجد رفع جماعي تلقائي.",14));
  CheckBox community=new CheckBox(this);community.setText("تنزيل التصحيحات العامة بعد مراجعتها تلقائيًا — لا يرفع سجلك");community.setTextColor(Ui.TEXT);community.setChecked(getSharedPreferences("reading",0).getBoolean("communityUpdates",true));community.setOnCheckedChangeListener((b,on)->{getSharedPreferences("reading",0).edit().putBoolean("communityUpdates",on).apply();if(on)CommunityCorrections.refresh(this);});root.addView(community);
  Ui.button(this,root,"تحديث التصحيحات المراجعة الآن",false,()->{status.setText("جارٍ تحديث التصحيحات؛ لا يُرفع السجل.");CommunityCorrections.refresh(this,true,()->{if(!isFinishing()&&!isDestroyed())status.setText(CommunityCorrections.syncStatus);});});
  CheckBox enabled=new CheckBox(this);enabled.setText("حفظ سجل النصوص محليًا");enabled.setTextColor(Ui.TEXT);enabled.setChecked(getSharedPreferences("reading",0).getBoolean("history",true));enabled.setOnCheckedChangeListener((b,on)->getSharedPreferences("reading",0).edit().putBoolean("history",on).apply());root.addView(enabled);
  Ui.button(this,root,"تصدير سجل المقارنة مع الأوقات",false,this::exportLog);
  Ui.button(this,root,"تصدير حزمة اللعبة الحالية",false,()->new AlertDialog.Builder(this).setTitle("تصدير حزمة اللعبة الحالية").setItems(new String[]{"السطور التي صححتها فقط","الكل — ترجمة آلية تحتاج مراجعة"},(d,which)->export(which==0)).show());
  Ui.button(this,root,"مسح السجل المحلي",false,()->new AlertDialog.Builder(this).setMessage("مسح جميع أسطر السجل من هذا الجوال؟").setPositiveButton("مسح",(d,w)->{try{DialogueStore.get(this).clear();render();}catch(Exception e){status.setText("تعذر مسح السجل؛ أعد المحاولة.");}}).setNegativeButton("إلغاء",null).show());
  status=Ui.text(this,"",13);root.addView(status);rows=Ui.column(this);root.addView(rows);Ui.button(this,root,"رجوع",false,this::finish);
  ScrollView scroll=new ScrollView(this);scroll.addView(root);PhoneInsets.fit(scroll);setContentView(scroll);render();
 }
 private void render(){rows.removeAllViews();JSONArray lines=DialogueStore.get(this).snapshot();status.setText("عدد الأسطر: "+lines.length());
  for(int i=lines.length()-1;i>=0;i--){JSONObject entry=lines.optJSONObject(i);if(entry==null)continue;
   LinearLayout card=Ui.card(this,rows,entry.optString("game")+" · "+entry.optString("mode")+(entry.optBoolean("reviewed")?" · مراجع":""));
   TextView english=Ui.text(this,entry.optString("source"),15);english.setTextDirection(android.view.View.TEXT_DIRECTION_LTR);english.setTextIsSelectable(true);card.addView(english);
   String warning=TranslationQuality.warning(entry.optString("source"),entry.optString("arabic"));if(!warning.isEmpty())card.addView(Ui.text(this,"⚠ "+warning,13));
   TextView arabic=Ui.text(this,entry.optString("arabic"),18);arabic.setTextIsSelectable(true);card.addView(arabic);card.addView(Ui.text(this,entry.optString("engine")+" · قراءة "+entry.optLong("read_ms")+" ms · ترجمة "+entry.optLong("translation_ms")+" ms · إجمالي "+entry.optLong("total_ms")+" ms",12));
   Ui.button(this,card,entry.optBoolean("reported")?"إعادة فتح بلاغ هذا السطر":"الإبلاغ عن خطأ لغوي للمراجعة",false,()->report(entry));
   Ui.button(this,card,"تصحيح واعتماد هذا السطر",false,()->{EditText edit=new EditText(this);edit.setText(entry.optString("arabic"));new AlertDialog.Builder(this).setTitle("تصحيح الترجمة العربية").setView(edit).setPositiveButton("اعتماد",(d,w)->{try{DialogueStore.get(this).revise(entry.optLong("id"),edit.getText().toString());render();}catch(Exception e){status.setText("تعذر حفظ التصحيح؛ افحص النص والمساحة المتاحة.");}}).setNegativeButton("إلغاء",null).show();});
  }
 }
 private void report(JSONObject entry){
  String body="اللعبة: "+entry.optString("game")+"\nالمحرك: "+entry.optString("engine")+"\nEnglish:\n"+entry.optString("source")+"\nالعربية الحالية:\n"+entry.optString("original_arabic",entry.optString("arabic"))+"\nالتصحيح المقترح: "+(entry.optBoolean("reviewed")?entry.optString("arabic"):"غير محدد");
  new AlertDialog.Builder(this).setTitle("مراجعة البلاغ قبل نشره").setMessage(body+"\n\nسيُفتح GitHub لإرسال البلاغ من حسابك. لا ننشره نيابة عنك ولا نرسل السجل كاملًا.").setPositiveButton("نسخ البلاغ وفتح صفحة إرساله",(d,w)->{((android.content.ClipboardManager)getSystemService(CLIPBOARD_SERVICE)).setPrimaryClip(ClipData.newPlainText("بلاغ ترجمة",body));DialogueStore.get(this).flag(entry.optLong("id"));try{startActivity(new Intent(Intent.ACTION_VIEW,android.net.Uri.parse("https://github.com/Tariq-220/ArabicGamesTranslator/issues/new?template=translation-error.yml")));status.setText("انسخ بيانات البلاغ في GitHub واضغط Submit. وُسِم السطر محليًا للمراجعة؛ لم يُرسل تلقائيًا.");}catch(ActivityNotFoundException e){status.setText("نُسخ البلاغ؛ افتح صفحة Issues في مستودع التطبيق لإرساله.");}render();}).setNegativeButton("إلغاء",null).show();
 }
 private void exportLog(){try{JSONArray entries=DialogueStore.get(this).snapshot();StringBuilder jsonl=new StringBuilder();jsonl.append(new JSONObject().put("type","session-metrics").put("provider",AiPreferences.provider(this)).put("last_line",Status.metrics).put("last_inference",Status.cloudMetrics).put("frames",Status.framesRead).toString()).append('\n');for(int i=0;i<entries.length();i++)jsonl.append(entries.getJSONObject(i).toString()).append('\n');pendingExport=jsonl.toString();exportingLog=true;startActivityForResult(new Intent(Intent.ACTION_CREATE_DOCUMENT).setType("application/octet-stream").addCategory(Intent.CATEGORY_OPENABLE).putExtra(Intent.EXTRA_TITLE,"comparison-phone.jsonl"),801);}catch(Exception e){status.setText("تعذر تصدير السجل.");}}
 private void export(boolean reviewed){try{exportingLog=false;String game=AiPreferences.game(this);pendingExport=DialogueStore.get(this).pack(game,reviewed);startActivityForResult(new Intent(Intent.ACTION_CREATE_DOCUMENT).setType("application/json").addCategory(Intent.CATEGORY_OPENABLE).putExtra(Intent.EXTRA_TITLE,"ArabicGames-Pack.json"),801);}catch(Exception e){status.setText("لا توجد سطور مناسبة للعبة المختارة. صحّح سطرًا أو اختر تصدير الكل، وتأكد من اسم اللعبة في إعدادات المحرك.");}}
 @Override protected void onActivityResult(int request,int result,Intent data){super.onActivityResult(request,result,data);if(request!=801)return;if(result==RESULT_OK&&data!=null&&pendingExport!=null){try(java.io.OutputStream out=getContentResolver().openOutputStream(data.getData())){out.write(pendingExport.getBytes(java.nio.charset.StandardCharsets.UTF_8));status.setText(exportingLog?"تم تصدير السجل مع أوقات القراءة والترجمة. لا يحتوي مفاتيح أو بيانات ربط.":"تم التصدير. يمكنك استيراد الحزمة من الصفحة الرئيسية لتسريع السطور المعروفة.");}catch(Exception e){status.setText("تعذر حفظ ملف الحزمة.");}}pendingExport=null;}
}
