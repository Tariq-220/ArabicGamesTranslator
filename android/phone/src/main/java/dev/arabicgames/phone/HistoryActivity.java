// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames.phone;
import android.app.*;
import android.content.*;
import android.os.Bundle;
import android.widget.*;
import org.json.*;

public final class HistoryActivity extends Activity {
 private LinearLayout rows;private TextView status;private String pendingExport;
 @Override public void onCreate(Bundle saved){super.onCreate(saved);
  LinearLayout root=Ui.column(this);root.setBackgroundColor(Ui.BG);root.setPadding(Ui.dp(this,16),Ui.dp(this,16),Ui.dp(this,16),Ui.dp(this,16));root.addView(Ui.text(this,"سجل الحوارات والترجمة",24));
  root.addView(Ui.text(this,"آخر 200 سطر محفوظة على هذا الجوال فقط. صحّح السطر قبل تصديره كحزمة مراجعة. السطور الآلية قد تكون خاطئة. لا يوجد رفع جماعي تلقائي.",14));
  CheckBox enabled=new CheckBox(this);enabled.setText("حفظ سجل النصوص محليًا");enabled.setTextColor(Ui.TEXT);enabled.setChecked(getSharedPreferences("reading",0).getBoolean("history",true));enabled.setOnCheckedChangeListener((b,on)->getSharedPreferences("reading",0).edit().putBoolean("history",on).apply());root.addView(enabled);
  Ui.button(this,root,"تصدير حزمة اللعبة الحالية",false,()->new AlertDialog.Builder(this).setTitle("تصدير حزمة اللعبة الحالية").setItems(new String[]{"السطور التي صححتها فقط","الكل — ترجمة آلية تحتاج مراجعة"},(d,which)->export(which==0)).show());
  Ui.button(this,root,"مسح السجل المحلي",false,()->new AlertDialog.Builder(this).setMessage("مسح جميع أسطر السجل من هذا الجوال؟").setPositiveButton("مسح",(d,w)->{DialogueStore.get(this).clear();render();}).setNegativeButton("إلغاء",null).show());
  status=Ui.text(this,"",13);root.addView(status);rows=Ui.column(this);root.addView(rows);Ui.button(this,root,"رجوع",false,this::finish);
  ScrollView scroll=new ScrollView(this);scroll.addView(root);PhoneInsets.fit(scroll);setContentView(scroll);render();
 }
 private void render(){rows.removeAllViews();JSONArray lines=DialogueStore.get(this).snapshot();status.setText("عدد الأسطر: "+lines.length());
  for(int i=lines.length()-1;i>=0;i--){JSONObject entry=lines.optJSONObject(i);if(entry==null)continue;
   LinearLayout card=Ui.card(this,rows,entry.optString("game")+" · "+entry.optString("mode")+(entry.optBoolean("reviewed")?" · مراجع":""));
   TextView english=Ui.text(this,entry.optString("source"),15);english.setTextDirection(android.view.View.TEXT_DIRECTION_LTR);english.setTextIsSelectable(true);card.addView(english);
   TextView arabic=Ui.text(this,entry.optString("arabic"),18);arabic.setTextIsSelectable(true);card.addView(arabic);card.addView(Ui.text(this,entry.optString("engine"),12));
   Ui.button(this,card,"تصحيح واعتماد هذا السطر",false,()->{EditText edit=new EditText(this);edit.setText(entry.optString("arabic"));new AlertDialog.Builder(this).setTitle("تصحيح الترجمة العربية").setView(edit).setPositiveButton("اعتماد",(d,w)->{try{DialogueStore.get(this).revise(entry.optLong("id"),edit.getText().toString());render();}catch(Exception e){status.setText("النص فارغ أو طويل جدًا.");}}).setNegativeButton("إلغاء",null).show();});
  }
 }
 private void export(boolean reviewed){try{String game=AiPreferences.game(this);pendingExport=DialogueStore.get(this).pack(game,reviewed);startActivityForResult(new Intent(Intent.ACTION_CREATE_DOCUMENT).setType("application/json").addCategory(Intent.CATEGORY_OPENABLE).putExtra(Intent.EXTRA_TITLE,"ArabicGames-Pack.json"),801);}catch(Exception e){status.setText("لا توجد سطور مناسبة للعبة المختارة. صحّح سطرًا أو اختر تصدير الكل، وتأكد من اسم اللعبة في إعدادات المحرك.");}}
 @Override protected void onActivityResult(int request,int result,Intent data){super.onActivityResult(request,result,data);if(request!=801)return;if(result==RESULT_OK&&data!=null&&pendingExport!=null){try(java.io.OutputStream out=getContentResolver().openOutputStream(data.getData())){out.write(pendingExport.getBytes(java.nio.charset.StandardCharsets.UTF_8));status.setText("تم التصدير. يمكنك استيراد الحزمة من الصفحة الرئيسية لتسريع السطور المعروفة.");}catch(Exception e){status.setText("تعذر حفظ ملف الحزمة.");}}pendingExport=null;}
}
