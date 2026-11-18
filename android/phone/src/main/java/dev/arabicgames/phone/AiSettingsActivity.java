// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames.phone;
import android.app.Activity;
import android.os.Bundle;
import android.text.InputType;
import android.view.*;
import android.widget.*;

public final class AiSettingsActivity extends Activity {
 private final String[] providers={AiPreferences.LOCAL,AiPreferences.GEMINI,AiPreferences.OPENAI,AiPreferences.DEEPL,AiPreferences.LAN,AiPreferences.GOOGLE,AiPreferences.NATIVE};
 private Spinner provider,modelChoice;private EditText model,game,key,address;private CheckBox consent;private TextView feedback;
 private String editingProvider;
 private final java.util.Map<String,String> keyDrafts=new java.util.HashMap<>(),modelDrafts=new java.util.HashMap<>();
 private TextView keyCaption;private Button apiLink;
 private CheckBox fast;
 private Button saveButton;
 private static final class Draft {
  final java.util.Map<String,String> keys=new java.util.HashMap<>(),models=new java.util.HashMap<>();
  String provider,game,address;boolean consent,fast;
 }
 @Override public void onCreate(Bundle saved){
  super.onCreate(saved);getWindow().setStatusBarColor(Ui.BG);getWindow().setNavigationBarColor(Ui.BG);
  Draft retained=(Draft)getLastNonConfigurationInstance();if(retained!=null){keyDrafts.putAll(retained.keys);modelDrafts.putAll(retained.models);}
  getWindow().addFlags(WindowManager.LayoutParams.FLAG_SECURE);
  LinearLayout root=Ui.column(this);root.setPadding(Ui.dp(this,16),Ui.dp(this,16),Ui.dp(this,16),Ui.dp(this,20));root.setBackgroundColor(Ui.BG);
  root.addView(Ui.text(this,"محرك الترجمة",26));
  root.addView(Ui.text(this,"المحلي بلا إنترنت. Gemini وOpenAI وDeepL تترجم نص الحوار مع السياق؛ لا نرسل صور اللعبة. تحتاج مفتاح API خاصًا بالخدمة، وقد تُحتسب تكلفة حسب حسابك. الترجمة تحتاج تجربة فعلية للمقارنة.",15));
  root.addView(Ui.text(this,"المحرك",16));provider=new Spinner(this);
  provider.setAdapter(optionAdapter(new String[]{"محلي — بدون إنترنت","Gemini — سحابي","OpenAI — سحابي","DeepL — ترجمة متخصصة","QuickMT / Jisr على الكمبيوتر","Google — إنترنت بدون مفتاح","QuickMT / جسر — على الجوال بدون إنترنت"}));LinearLayout engineRow=new LinearLayout(this);engineRow.setOrientation(LinearLayout.HORIZONTAL);engineRow.setLayoutDirection(View.LAYOUT_DIRECTION_RTL);
  engineRow.addView(provider,new LinearLayout.LayoutParams(0,-2,1));
  apiLink=Ui.button(this,engineRow,"API ↗",false,()->{
   String selected=providers[provider.getSelectedItemPosition()];String url;
   if(AiPreferences.NATIVE.equals(selected)){startActivity(new android.content.Intent(this,LocalModelsActivity.class));return;}
   if(AiPreferences.GEMINI.equals(selected))url="https://aistudio.google.com/apikey";
   else if(AiPreferences.OPENAI.equals(selected))url="https://platform.openai.com/api-keys";
   else if(AiPreferences.DEEPL.equals(selected))url="https://www.deepl.com/your-account/keys";
   else {feedback.setText("المحرك المختار لا يحتاج مفتاح خدمة سحابية. QuickMT عبر الكمبيوتر يحتاج رمز خادمك المحلي.");return;}
   try{startActivity(new android.content.Intent(android.content.Intent.ACTION_VIEW,android.net.Uri.parse(url)));}catch(android.content.ActivityNotFoundException e){feedback.setText("ثبّت متصفحًا لفتح صفحة المفتاح.");}
  });
  apiLink.setLayoutParams(new LinearLayout.LayoutParams(-2,-2));apiLink.setMinWidth(Ui.dp(this,76));
  root.addView(engineRow);
  address=field(root,InputType.TYPE_CLASS_TEXT|InputType.TYPE_TEXT_VARIATION_URI);address.setHint("عنوان خادم كمبيوترك: http://192.168.0.10:8790");address.setText(AiPreferences.prefs(this).getString("lanAddress",""));address.setVisibility(View.GONE);
  root.addView(Ui.text(this,"اختر «على الجوال» للتشغيل المحلي، أو «على الكمبيوتر» لاستخدام خادم اللابتوب. الاتصال بالخادم المنزلي يحتاج رمز الربط فقط.",13));
  Ui.button(this,root,"تنزيل / إدارة QuickMT وجسر على الجوال",false,()->startActivity(new android.content.Intent(this,LocalModelsActivity.class)));
  root.addView(Ui.text(this,"اختر النموذج من القائمة — لا تحتاج كتابة اسمه",15));
  modelChoice=new Spinner(this);modelChoice.setTag("model-choice");modelChoice.setMinimumHeight(Ui.dp(this,52));modelChoice.setBackground(Ui.background(this,Ui.CARD));root.addView(modelChoice,new LinearLayout.LayoutParams(-1,-2));
  model=field(root,InputType.TYPE_CLASS_TEXT);model.setHint("اسم نموذج مخصص — خيار متقدم فقط");model.setVisibility(View.GONE);
  root.addView(Ui.text(this,"اسم اللعبة للسياق",15));game=field(root,InputType.TYPE_CLASS_TEXT);game.setText(AiPreferences.prefs(this).getString("game","Red Dead Redemption 2"));
  keyCaption=Ui.text(this,"مفتاح API — داخل التطبيق فقط، وليس في المحادثة",15);root.addView(keyCaption);key=field(root,InputType.TYPE_CLASS_TEXT|InputType.TYPE_TEXT_VARIATION_PASSWORD);
  key.setTag("api-key");model.setTag("api-model");provider.setTag("api-provider");key.setSaveEnabled(false);key.setImportantForAutofill(View.IMPORTANT_FOR_AUTOFILL_NO_EXCLUDE_DESCENDANTS);
  consent=new CheckBox(this);consent.setText("أوافق على إرسال نص الحوار والسياق إلى الخدمة المختارة وتطبيق تكلفتها وسياساتها.");consent.setTextColor(Ui.TEXT);consent.setChecked(AiPreferences.cloud(this));root.addView(consent);
  fast=new CheckBox(this);fast.setText("وضع أسرع لـGemini: سياق سطر واحد بدل ثلاثة؛ قد تقل دقة ربط الحوار. سرعة الخدمة غير مضمونة.");fast.setTextColor(Ui.TEXT);fast.setChecked(AiPreferences.prefs(this).getBoolean("geminiFast",false));root.addView(fast);
  if(retained!=null){game.setText(retained.game);address.setText(retained.address);consent.setChecked(retained.consent);fast.setChecked(retained.fast);}
  Ui.button(this,root,"تجربة Gemini Flash لجودة الحوار",false,()->{
   provider.setSelection(1);provider.post(()->{model.setText("gemini-3.8-flash");modelDrafts.put(AiPreferences.GEMINI,"gemini-3.8-flash");showModelChoices(AiPreferences.GEMINI,"gemini-3.8-flash");fast.setChecked(false);feedback.setText("إعداد تجربة: Gemini 3.8 Flash مع سياق 3 سطور وتفكير low. يحتاج مفتاحك وموافقتك؛ التكلفة والسرعة قد تختلف. احفظ أو استخدم للجلسة، ثم قارن المشهد نفسه.");});
  });
  feedback=Ui.text(this,"المفتاح يُحفظ مشفّرًا على الجهاز. لا يُرسل إلى الشاشة. لا يوجد تبديل صامت إلى المحلي عند فشل AI.",14);root.addView(feedback);
  provider.setOnItemSelectedListener(new android.widget.AdapterView.OnItemSelectedListener(){
   public void onNothingSelected(android.widget.AdapterView<?> parent){}
   public void onItemSelected(android.widget.AdapterView<?> parent,View v,int position,long id){
    String p=providers[position];if(p.equals(editingProvider))return;
    if(editingProvider!=null){keyDrafts.put(editingProvider,key.getText().toString());modelDrafts.put(editingProvider,model.getText().toString());}
    editingProvider=p;boolean online=position!=0&&position!=6,needsKey=online&&!AiPreferences.GOOGLE.equals(p);model.setEnabled(needsKey&&!AiPreferences.DEEPL.equals(p));keyCaption.setVisibility(needsKey?View.VISIBLE:View.GONE);apiLink.setText(AiPreferences.NATIVE.equals(p)?"نماذج ↓":"API ↗");key.setEnabled(needsKey);key.setVisibility(online?View.VISIBLE:View.GONE);consent.setEnabled(online);consent.setVisibility(online?View.VISIBLE:View.GONE);
    address.setVisibility(AiPreferences.LAN.equals(p)?View.VISIBLE:View.GONE);
    model.setText(modelDrafts.getOrDefault(p,AiPreferences.prefs(AiSettingsActivity.this).getString(p+".model",AiPreferences.defaultModel(p))));key.setText(keyDrafts.getOrDefault(p,""));showModelChoices(p,model.getText().toString());
    boolean stored=!AiPreferences.prefs(AiSettingsActivity.this).getString(p+".key","").isEmpty();
    key.setHint(stored?"مفتاح محفوظ — اتركه فارغًا للاحتفاظ به":"أدخل مفتاح الخدمة المختارة");
    feedback.setText(AiPreferences.GOOGLE.equals(p)?"Google يرسل النص عبر الإنترنت بدون مفتاح، وليس Google Cloud API الرسمي. يحتاج موافقتك وقد يتوقف أو يحدد الطلبات. لا يُرسل سياق الحوارات السابقة.":AiPreferences.sessionOnly(p)?"المفتاح يعمل لهذه الجلسة فقط، وسيحتاج إدخالًا بعد إنهاء عملية التطبيق.":stored?"يوجد مفتاح محفوظ لـ"+AiPreferences.label(p)+". الخانة الفارغة لا تعني أنه انحذف؛ الصق مفتاحًا جديدًا لاستبداله.":online?"لا يوجد مفتاح محفوظ لـ"+AiPreferences.label(p)+". الصق المفتاح وفعّل الموافقة ثم احفظ.":"المحلي لا يحتاج مفتاح API.");
   }
  });
  String active=retained!=null?retained.provider:AiPreferences.provider(this);provider.setSelection(AiPreferences.NATIVE.equals(active)?6:AiPreferences.GOOGLE.equals(active)?5:AiPreferences.LAN.equals(active)?4:AiPreferences.DEEPL.equals(active)?3:AiPreferences.OPENAI.equals(active)?2:AiPreferences.GEMINI.equals(active)?1:0);
  saveButton=Ui.button(this,root,"حفظ المحرك والعودة",true,()->{
   if(provider.getSelectedItemPosition()!=0&&provider.getSelectedItemPosition()!=6&&!consent.isChecked()){
    new android.app.AlertDialog.Builder(this).setTitle("الموافقة على إرسال الحوار")
     .setMessage("سيرسل التطبيق نص حوار اللعبة وسياقه إلى "+AiPreferences.label(providers[provider.getSelectedItemPosition()])+" لترجمته. تطبق سياسة الخدمة وتكلفة حسابك. هل توافق على حفظ هذا الاختيار؟")
     .setPositiveButton("أوافق وأحفظ",(dialog,which)->{consent.setChecked(true);saveButton.performClick();})
     .setNegativeButton("إلغاء",null).show();return;
   }
   try{
    String gameName=game.getText().toString();if(gameName.length()>100)throw new IllegalArgumentException("اسم اللعبة طويل جدًا؛ استخدم أقل من 100 حرف.");
    if(provider.getSelectedItemPosition()==4)AiPreferences.prefs(this).edit().putString("lanAddress",LocalEndpoint.validate(address.getText().toString())).apply();
    AiPreferences.save(this,providers[provider.getSelectedItemPosition()],model.getText().toString().trim(),gameName,key.getText().toString(),consent.isChecked());
    AiPreferences.prefs(this).edit().putBoolean("geminiFast",fast.isChecked()).apply();
    key.setText("");CaptureService.translationChanged();Status.message="تم اختيار محرك "+AiPreferences.label(AiPreferences.provider(this))+". ابدأ قراءة اللعبة للمقارنة.";finish();
   }catch(IllegalArgumentException e){feedback.setText(e.getMessage());feedback.requestRectangleOnScreen(new android.graphics.Rect(0,0,feedback.getWidth(),feedback.getHeight()));}
    catch(Exception e){feedback.setText("تعذر التخزين المشفّر ("+e.getClass().getSimpleName()+"). المفتاح ما زال بالخانة. اختر «استخدام لهذه الجلسة فقط» أسفل زر الحفظ لتجاوز التخزين، وليس فحص صلاحية الخدمة.");}
  });
  Ui.button(this,root,"استخدام لهذه الجلسة فقط",false,()->{
   try{
    if(provider.getSelectedItemPosition()==0)throw new IllegalArgumentException("اختر Gemini أو OpenAI أو DeepL أولًا.");
    String endpoint=provider.getSelectedItemPosition()==4?LocalEndpoint.validate(address.getText().toString()):"";
    AiPreferences.useForSession(providers[provider.getSelectedItemPosition()],model.getText().toString().trim(),game.getText().toString(),key.getText().toString(),consent.isChecked(),fast.isChecked(),endpoint);
    key.setText("");CaptureService.translationChanged();Status.message="AI يعمل بمفتاح مؤقت في الذاكرة لهذه الجلسة فقط. لم يُحفظ على الجهاز.";finish();
   }catch(IllegalArgumentException e){feedback.setText(e.getMessage());}
  });
  Ui.button(this,root,"حذف المفاتيح والرجوع للمحلي",false,()->{AiPreferences.clear(this);keyDrafts.clear();modelDrafts.clear();key.setText("");CaptureService.translationChanged();provider.setSelection(0);consent.setChecked(false);feedback.setText("حُذفت مفاتيح الخدمات من التطبيق وعاد المحرك إلى المحلي.");});
  Ui.button(this,root,"رجوع دون حفظ",false,this::finish);
  ScrollView scroll=new ScrollView(this);scroll.addView(root);PhoneInsets.fit(scroll);setContentView(scroll);
 }
 private ArrayAdapter<String> optionAdapter(String[] values){
  return new ArrayAdapter<String>(this,android.R.layout.simple_spinner_dropdown_item,values){
   private View style(View view){TextView t=(TextView)view;t.setTextColor(Ui.TEXT);t.setBackgroundColor(Ui.CARD);t.setTextSize(15);t.setMinHeight(Ui.dp(AiSettingsActivity.this,48));t.setPadding(Ui.dp(AiSettingsActivity.this,10),Ui.dp(AiSettingsActivity.this,8),Ui.dp(AiSettingsActivity.this,10),Ui.dp(AiSettingsActivity.this,8));return t;}
   public View getView(int n,View old,ViewGroup parent){return style(super.getView(n,old,parent));}
   public View getDropDownView(int n,View old,ViewGroup parent){return style(super.getDropDownView(n,old,parent));}
  };
 }
 private void showModelChoices(String p,String desired){
  final String[] ids=ModelChoices.forProvider(p);boolean custom=ModelChoices.customAllowed(p);
  String[] labels=new String[ids.length+(custom?1:0)];System.arraycopy(ids,0,labels,0,ids.length);
  if(AiPreferences.DEEPL.equals(p)){labels[0]="DeepL — تلقائي";labels[1]="DeepL — أسرع (latency)";labels[2]="DeepL — تفضيل الجودة";}
  if(AiPreferences.LOCAL.equals(p))labels[0]="النموذج المحلي المدمج";
  if(custom)labels[ids.length]="نموذج مخصص… (متقدم)";
  int selected=-1;for(int i=0;i<ids.length;i++)if(ids[i].equals(desired))selected=i;
  if(selected<0)selected=custom?ids.length:0;
  modelChoice.setOnItemSelectedListener(null);modelChoice.setAdapter(optionAdapter(labels));modelChoice.setSelection(selected);
  modelChoice.setEnabled(ids.length>1||custom);model.setVisibility(selected==ids.length&&custom?View.VISIBLE:View.GONE);
  if(selected<ids.length)model.setText(ids[selected]);
  modelChoice.setOnItemSelectedListener(new AdapterView.OnItemSelectedListener(){
   public void onNothingSelected(AdapterView<?> view){}
   public void onItemSelected(AdapterView<?> view,View row,int n,long id){
    if(!p.equals(editingProvider))return;
    if(n<ids.length){model.setText(ids[n]);modelDrafts.put(p,ids[n]);model.setVisibility(View.GONE);}
    else{model.setVisibility(View.VISIBLE);model.requestFocus();}
   }
  });
 }
 @Override public Object onRetainNonConfigurationInstance(){
  if(editingProvider!=null){keyDrafts.put(editingProvider,key.getText().toString());modelDrafts.put(editingProvider,model.getText().toString());}
  Draft draft=new Draft();draft.keys.putAll(keyDrafts);draft.models.putAll(modelDrafts);draft.provider=providers[provider.getSelectedItemPosition()];draft.game=game.getText().toString();draft.address=address.getText().toString();draft.consent=consent.isChecked();draft.fast=fast.isChecked();return draft;
 }
 private EditText field(LinearLayout root,int input){EditText field=new EditText(this);field.setTextColor(Ui.TEXT);field.setHintTextColor(Ui.MUTED);field.setInputType(input);field.setSingleLine(true);field.setTextDirection(View.TEXT_DIRECTION_LTR);field.setMaxLines(1);root.addView(field,new LinearLayout.LayoutParams(-1,-2));return field;}
}
