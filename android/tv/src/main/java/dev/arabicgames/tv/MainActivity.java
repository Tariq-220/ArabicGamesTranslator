// SPDX-License-Identifier: AGPL-3.0-only
// TvView HDMI approach adapted from Sefflex/GameTranslator, derived from CouchSubs.
package dev.arabicgames.tv;
import android.app.*;
import android.content.*;
import android.graphics.Color;
import android.graphics.drawable.GradientDrawable;
import android.media.*;
import android.media.tv.*;
import android.os.*;
import android.text.InputType;
import android.view.*;
import android.widget.*;
import dev.arabicgames.Pairing;
import org.json.JSONObject;
import java.io.*;
import java.net.*;
import java.nio.charset.StandardCharsets;
import java.util.*;
import java.util.concurrent.*;

public final class MainActivity extends Activity {
 private boolean sourcePlacement;private String captionPositionKey="";private boolean videoAvailable;
 private boolean playRequested;
 private String tunedInput;private boolean resumed,restoreHdmi;private int resumeTicket;private TvInputManager inputManager;
 private final TvInputManager.TvInputCallback inputChanges=new TvInputManager.TvInputCallback(){
  @Override public void onInputAdded(String id){inputsChanged();}
  @Override public void onInputRemoved(String id){if(id.equals(tunedInput)){tunedInput=null;restoreHdmi=true;}inputsChanged();}
  @Override public void onInputUpdated(String id){inputsChanged();}
  @Override public void onInputStateChanged(String id,int state){inputsChanged();}
 };
 private TvView.TvInputCallback videoCallbacks;
 private TvView tv;private LinearLayout panel,connectionPanel;private ScrollView panelWindow;
 private TextView subtitle,status,received,hdmiStatus;private Button menuHandle;private int swallowedKey=-1;private android.window.OnBackInvokedCallback backCallback;
 private EditText host,code;private Spinner input;private CheckBox desktop,audio;
 private AudioManager audioManager;private AudioFocusRequest audioFocus;
 private final Handler ui=new Handler(Looper.getMainLooper());
 private final List<TvInputInfo> inputs=new ArrayList<>();
 private String receiverId,lastArabic="";private int drops,subtitleCount;
 private final ScheduledExecutorService poller=Executors.newSingleThreadScheduledExecutor();private ScheduledFuture<?> pollTask;
 private volatile HttpURLConnection pollingConnection;private long acceptedRevision,manualTestUntil;
 private boolean discovering;
 private volatile HttpURLConnection connection;private volatile int session;private volatile boolean destroyed;
 private final Runnable hide=()->subtitle.setVisibility(View.INVISIBLE);
 @Override public void onCreate(Bundle saved){super.onCreate(saved);getPreferences(0).edit().remove("resumeHdmi").apply();getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);
  receiverId=getPreferences(0).getString("receiverId",null);if(receiverId==null){receiverId=UUID.randomUUID().toString();getPreferences(0).edit().putString("receiverId",receiverId).apply();}
  audioManager=getSystemService(AudioManager.class);audioFocus=new AudioFocusRequest.Builder(AudioManager.AUDIOFOCUS_GAIN).setAudioAttributes(new AudioAttributes.Builder().setUsage(AudioAttributes.USAGE_MEDIA).setContentType(AudioAttributes.CONTENT_TYPE_MOVIE).build()).setOnAudioFocusChangeListener(change->{if(tv!=null)tv.setStreamVolume(resumed&&audio!=null&&audio.isChecked()?(change==AudioManager.AUDIOFOCUS_GAIN?1f:change==AudioManager.AUDIOFOCUS_LOSS_TRANSIENT_CAN_DUCK?.25f:0f):0f);},ui).build();
  FrameLayout root=new FrameLayout(this);root.setBackgroundColor(Color.BLACK);tv=new TvView(this);tv.setTag("hdmi-view");tv.setFocusable(false);tv.setFocusableInTouchMode(false);tv.setDescendantFocusability(ViewGroup.FOCUS_BLOCK_DESCENDANTS);root.addView(tv,new FrameLayout.LayoutParams(-1,-1));
  videoCallbacks=new TvView.TvInputCallback(){
   @Override public void onVideoAvailable(String id){if(!id.equals(tunedInput))return;restoreHdmi=false;videoAvailable=true;hdmiStatus.setText("✓ صورة HDMI جاهزة");trace("video available");if(resumed&&audio!=null)tv.setStreamVolume(audio.isChecked()?1f:0f);}
   @Override public void onConnectionFailed(String id){if(!id.equals(tunedInput))return;stopPlayback();hdmiStatus.setText("تعذر فتح المدخل داخل التطبيق؛ اختر HDMI الصحيح ثم اضغط عرض اللعبة.");}
   @Override public void onVideoUnavailable(String id,int reason){if(!id.equals(tunedInput))return;videoAvailable=false;trace("video unavailable: "+reason);hdmiStatus.setText("تغيّرت إشارة HDMI ("+reason+")؛ ننتظر عودة الصورة دون إعادة فتح المدخل.");}
   @Override public void onDisconnected(String id){if(!id.equals(tunedInput))return;stopPlayback();hdmiStatus.setText("انتهى اتصال HDMI. اختر المدخل واضغط عرض اللعبة للعودة.");}
  };tv.setCallback(videoCallbacks);
  subtitle=label("",20);subtitle.setMaxWidth((int)(getResources().getDisplayMetrics().widthPixels*.84));subtitle.setPadding(dp(12),dp(5),dp(12),dp(5));subtitle.setGravity(Gravity.CENTER);subtitle.setTextDirection(View.TEXT_DIRECTION_RTL);subtitle.setVisibility(View.INVISIBLE);applySubtitleStyle();
  FrameLayout.LayoutParams sub=new FrameLayout.LayoutParams(-2,-2,Gravity.BOTTOM|Gravity.CENTER_HORIZONTAL);sub.bottomMargin=dp(28);root.addView(subtitle,sub);applySubtitleStyle();
  panel=column();panel.setPadding(dp(14),dp(12),dp(14),dp(12));GradientDrawable bg=new GradientDrawable();bg.setColor(0xf51d293a);bg.setCornerRadius(dp(14));panel.setBackground(bg);
  panel.addView(label("الترجمة العربية · 0.3.14",18));status=label("اربط الجوال لبدء الاستقبال.",13);status.setMaxLines(3);panel.addView(status);
  received=label("سطور مترجمة مستلمة: 0",12);received.setTextColor(0xff72e2b1);panel.addView(received);
  button(panel,"اكتشاف الجوال أو الكمبيوتر",this::discoverDesktop);
  button(panel,"إعدادات الربط ▾",()->{connectionPanel.setVisibility(connectionPanel.getVisibility()==View.VISIBLE?View.GONE:View.VISIBLE);if(connectionPanel.getVisibility()==View.VISIBLE)host.requestFocus();});
  connectionPanel=column();host=new EditText(this);host.setHint("عنوان الجوال مثل 192.168.0.118");host.setTextSize(14);host.setSingleLine();host.setTextDirection(View.TEXT_DIRECTION_LTR);host.setInputType(InputType.TYPE_CLASS_PHONE);host.setText(getPreferences(0).getString("host",""));connectionPanel.addView(host);
  code=new EditText(this);code.setHint("رمز الربط — 8 أرقام");code.setTextSize(14);code.setSingleLine();code.setTextDirection(View.TEXT_DIRECTION_LTR);code.setInputType(InputType.TYPE_CLASS_NUMBER);code.setText(getPreferences(0).getString("code",""));connectionPanel.addView(code);
  desktop=new CheckBox(this);desktop.setText("مصدر كمبيوتر (بدون رمز الجوال)");desktop.setTextSize(12);desktop.setOnCheckedChangeListener((b,c)->code.setEnabled(!c));desktop.setChecked(getPreferences(0).getBoolean("desktop",false));connectionPanel.addView(desktop);
  button(connectionPanel,"حفظ وربط الجوال",this::connect);panel.addView(connectionPanel);
  connectionPanel.setVisibility(host.getText().toString().isEmpty()||!desktop.isChecked()&&!Pairing.validCode(code.getText().toString())?View.VISIBLE:View.GONE);
  input=new Spinner(this);refreshInputs();panel.addView(input);hdmiStatus=label("اختر HDMI ثم عرض اللعبة.",12);panel.addView(hdmiStatus);
  button(panel,"عرض اللعبة عبر HDMI",this::requestPlayback);
  button(panel,"إيقاف عرض HDMI والرجوع للإعدادات",this::stopPlayback);
  button(panel,"نسخ تقرير العرض",()->{((android.content.ClipboardManager)getSystemService(CLIPBOARD_SERVICE)).setPrimaryClip(ClipData.newPlainText("تقرير العرض","Google TV 0.3.14 / Android "+Build.VERSION.SDK_INT+"\n"+getPreferences(0).getString("displayTrace","")));status.setText("نُسخ تقرير العرض؛ لا يحتوي عنوان الشبكة أو رمز الربط.");});
  button(panel,"إعادة ضبط العرض داخل التطبيق",()->{stopPlayback();hdmiStatus.setText("أُعيد ضبط العرض. اختر مدخل HDMI الصحيح ثم اضغط عرض اللعبة.");});
  audio=new CheckBox(this);audio.setText("تشغيل صوت HDMI من الشاشة");audio.setTextSize(13);audio.setChecked(getPreferences(0).getBoolean("hdmiAudio",true));audio.setOnCheckedChangeListener((b,on)->{getPreferences(0).edit().putBoolean("hdmiAudio",on).apply();restoreAudio();});panel.addView(audio);
  LinearLayout actions=new LinearLayout(this);actions.setOrientation(LinearLayout.HORIZONTAL);actions.setLayoutDirection(View.LAYOUT_DIRECTION_RTL);
  Button test=button(actions,"اختبار العربية",()->testLocal("اختبار محلي للشاشة — الحروف متصلة؟"));test.setLayoutParams(new LinearLayout.LayoutParams(0,-2,1));
  Button close=button(actions,"عودة للعب",this::hidePanel);close.setLayoutParams(new LinearLayout.LayoutParams(0,-2,1));panel.addView(actions);
  TextView hint=label("الربط لا يفتح HDMI · عرض اللعبة يدوي · رجوع يفتح اللوحة أثناء اللعب",11);hint.setTextColor(0xffa9b8ca);connectionPanel.addView(hint);
  button(connectionPanel,"خروج من التطبيق",this::finish);
  button(panel,"حجم وخلفية الترجمة",this::subtitleSettings);
  panelWindow=new PanelScroll(this);panelWindow.addView(panel);int w=Math.min(dp(340),(int)(getResources().getDisplayMetrics().widthPixels*.46));FrameLayout.LayoutParams pp=new FrameLayout.LayoutParams(w,-2,Gravity.TOP|Gravity.RIGHT);pp.setMargins(dp(16),dp(16),dp(16),dp(16));root.addView(panelWindow,pp);
  // Main panel stays small; setup and diagnostics are behind one explicit button.
  List<View> oldPanel=new ArrayList<>();for(int n=0;n<panel.getChildCount();n++)oldPanel.add(panel.getChildAt(n));
  panel.removeAllViews();panel.addView(oldPanel.get(0));panel.addView(status);panel.addView(received);
  LinearLayout settings=column();settings.setTag("tv-settings");settings.setVisibility(View.GONE);
  for(View view:oldPanel){
   if(view==oldPanel.get(0)||view==status||view==received||view==actions)continue;
   if(view instanceof Button&&("إعدادات الربط ▾".contentEquals(((Button)view).getText())||"اكتشاف الجوال أو الكمبيوتر".contentEquals(((Button)view).getText())))continue;
   settings.addView(view);
  }
  connectionPanel.setVisibility(View.GONE);
  button(panel,"اكتشاف الجوال أو الكمبيوتر",this::discoverDesktop);
  button(settings,"عنوان ورمز يدوي — بديل",()->connectionPanel.setVisibility(connectionPanel.getVisibility()==View.VISIBLE?View.GONE:View.VISIBLE));
  button(panel,"عرض اللعبة / إخفاء اللوحة",()->requestPlayback());
  button(panel,"إصلاح ظهور الترجمة",this::repairDisplay);
  button(panel,"الإعدادات: الربط والخط والصوت",()->settings.setVisibility(settings.getVisibility()==View.VISIBLE?View.GONE:View.VISIBLE));
  button(settings,"التحديثات — فحص وتنزيل تلقائي",()->startActivity(new android.content.Intent(this,dev.arabicgames.update.UpdateActivity.class)));
  button(settings,"اختبار الحروف العربية",()->testLocal("اختبار العربية — الحروف متصلة فوق اللعبة"));panel.addView(settings);
  menuHandle=new Button(this);menuHandle.setText("إعدادات الترجمة ⋮");menuHandle.setTextSize(12);menuHandle.setTag("menu-handle");menuHandle.setOnClickListener(v->showPanel());menuHandle.setVisibility(View.GONE);
  FrameLayout.LayoutParams handle=new FrameLayout.LayoutParams(dp(170),dp(44),Gravity.TOP|Gravity.RIGHT);handle.setMargins(dp(12),dp(12),dp(12),dp(12));root.addView(menuHandle,handle);
  setContentView(root);showPanel();dev.arabicgames.update.UpdateActivity.onLaunch(this,"TV");
  if(Build.VERSION.SDK_INT>=33){backCallback=()->{if(panelWindow.getVisibility()==View.VISIBLE)hidePanel();else showPanel();};getOnBackInvokedDispatcher().registerOnBackInvokedCallback(android.window.OnBackInvokedDispatcher.PRIORITY_DEFAULT,backCallback);}
  if(!host.getText().toString().isEmpty()&&(desktop.isChecked()||Pairing.validCode(code.getText().toString())))connect();
  if(host.getText().toString().isEmpty())discoverDesktop();
  inputManager=(TvInputManager)getSystemService(TV_INPUT_SERVICE);if(inputManager!=null)inputManager.registerCallback(inputChanges,ui);
  // Start in the controllable panel. Saved HDMI selection is restored without auto-tuning.
  if(inputs.isEmpty())hdmiStatus.setText("لا توجد مداخل HDMI متاحة للتطبيق على هذه الشاشة.");
 }
 private void discoverDesktop(){
  if(discovering||destroyed)return;discovering=true;status.setText("جارٍ البحث عن الجوال والكمبيوتر على الشبكة المحلية…");
  new Thread(()->{List<String> found;try{found=DesktopDiscovery.find();}catch(Exception e){found=Collections.emptyList();}final List<String> results=found;
   ui.post(()->{discovering=false;if(destroyed)return;
    if(results.isEmpty()){status.setText("لم نجد مصدرًا. شغّل الربط بالجوال أو مترجم الكمبيوتر، وتأكد من نفس الشبكة؛ العنوان اليدوي بديل.");return;}
    if(results.size()==1){useSource(results.get(0));return;}
    new AlertDialog.Builder(this).setTitle("اختر مصدر الترجمة").setItems(results.toArray(new String[0]),(d,n)->useSource(results.get(n))).setNegativeButton("إلغاء",null).show();
   });
  },"discover-desktop").start();
 }
 private int pairingAttempt;
 private void useSource(String result){String address=result.substring(result.indexOf(" · ")+3);if(result.startsWith("جوال"))pairPhone(address);else useDesktop(address);}
 private void pairPhone(String address){
  final int attempt=++pairingAttempt;String ticket=UUID.randomUUID().toString().replace("-","");
  status.setText("افتح تطبيق الجوال ووافق على رمز الربط: "+ticket.substring(0,6));
  new Thread(()->{for(int n=0;n<70&&!destroyed&&attempt==pairingAttempt;n++){
   HttpURLConnection c=null;try{
    c=(HttpURLConnection)new URL("http://"+address+":8787/pair?ticket="+ticket).openConnection();c.setConnectTimeout(1500);c.setReadTimeout(1500);c.setInstanceFollowRedirects(false);
    if(c.getResponseCode()!=200)break;String body;try(InputStream in=c.getInputStream()){ByteArrayOutputStream out=new ByteArrayOutputStream();byte[] b=new byte[128];int count;while((count=in.read(b))!=-1){out.write(b,0,count);if(out.size()>1024)throw new IOException();}body=out.toString("UTF-8");}
    String paired=new JSONObject(body).optString("code","");if(Pairing.validCode(paired)){ui.post(()->{if(destroyed||attempt!=pairingAttempt)return;host.setText(address);code.setText(paired);desktop.setChecked(false);connect();});return;}
    Thread.sleep(750);
   }catch(Exception e){break;}finally{if(c!=null)c.disconnect();}
  }ui.post(()->{if(!destroyed&&attempt==pairingAttempt)status.setText("لم يكتمل الربط. افتح الجوال ووافق على الرمز، ثم أعد الاكتشاف.");});},"pair-phone").start();
 }
 private void useDesktop(String address){host.setText(address);desktop.setChecked(true);connect();}
 private void refreshInputs(){
  String preferred=null;int selected=input==null?-1:input.getSelectedItemPosition();if(selected>=0&&selected<inputs.size())preferred=inputs.get(selected).getId();
  inputs.clear();List<String> labels=new ArrayList<>();TvInputManager manager=(TvInputManager)getSystemService(TV_INPUT_SERVICE);
  List<TvInputInfo> list=manager==null?null:manager.getTvInputList();if(list!=null)for(TvInputInfo i:list)if(i.getType()==TvInputInfo.TYPE_HDMI&&i.isPassthroughInput()){inputs.add(i);labels.add(i.loadLabel(this).toString());}
  if(labels.isEmpty())labels.add("لا توجد مداخل HDMI");input.setAdapter(new ArrayAdapter<>(this,android.R.layout.simple_spinner_dropdown_item,labels));
  String saved=preferred!=null?preferred:getPreferences(0).getString("hdmi","");for(int i=0;i<inputs.size();i++)if(inputs.get(i).getId().equals(saved))input.setSelection(i);
 }
 private void trace(String event){String log=getPreferences(0).getString("displayTrace","")+"\n"+SystemClock.elapsedRealtime()+" "+event;if(log.length()>2500)log=log.substring(log.length()-2500);getPreferences(0).edit().putString("displayTrace",log).apply();}
 private boolean requestPlayback(){trace("manual play");playRequested=true;boolean started=tune();if(started)hidePanel();else playRequested=false;return started;}
 private void stopPlayback(){playRequested=false;resumeTicket++;releaseHdmiSession();showPanel();}
 private boolean tune(){
  if(!playRequested)return false;
  if(inputs.isEmpty())refreshInputs();
  if(inputs.isEmpty()){hdmiStatus.setText("بانتظار تهيئة مداخل HDMI. المدخل المحفوظ لم يُحذف؛ أعد المحاولة بعد لحظات.");showPanel();return false;}
  try{int index=input.getSelectedItemPosition();if(index<0||index>=inputs.size())return false;TvInputInfo selected=inputs.get(index);restoreAudio();if(selected.getId().equals(tunedInput))return true;
   getPreferences(0).edit().putString("hdmi",selected.getId()).apply();tunedInput=selected.getId();restoreHdmi=true;tv.tune(tunedInput,TvContract.buildChannelUriForPassthroughInput(tunedInput));trace("tune submitted");dev.arabicgames.update.UpdateActivity.setPlaySessionActive(true);return true;
  }catch(SecurityException e){tunedInput=null;hdmiStatus.setText("نظام الشاشة يمنع فتح HDMI من التطبيق.");}catch(Exception e){tunedInput=null;hdmiStatus.setText("تعذر فتح المدخل المختار؛ لم يُحذف المدخل المحفوظ.");}showPanel();return false;
 }
 private void inputsChanged(){if(destroyed||input==null)return;refreshInputs();}

 private void restoreAudio(){if(tv==null||audio==null)return;if(!resumed){tv.setStreamVolume(0f);return;}if(audio.isChecked()){audioManager.requestAudioFocus(audioFocus);tv.setStreamVolume(1f);}else{tv.setStreamVolume(0f);audioManager.abandonAudioFocusRequest(audioFocus);}}
 private void connect(){
  String address=host.getText().toString().trim(),pair=desktop.isChecked()?null:code.getText().toString().trim();
  if(!Pairing.privateIpv4(address)||(pair!=null&&!Pairing.validCode(pair))){status.setText("أدخل IPv4 محليًا ورمز الجوال كاملًا.");return;}
  getPreferences(0).edit().putString("host",address).putString("code",pair==null?"":pair).putBoolean("desktop",desktop.isChecked()).apply();int generation=++session;HttpURLConnection old=connection;if(old!=null)old.disconnect();status.setText(desktop.isChecked()?"جارٍ إعادة ربط الكمبيوتر المحفوظ…":"جارٍ إعادة ربط الجوال المحفوظ…");acceptedRevision=0;if(pollTask!=null)pollTask.cancel(false);if(pollingConnection!=null)pollingConnection.disconnect();
  if(pair!=null)pollTask=poller.scheduleWithFixedDelay(()->pollLatest(address,pair,generation),0,500,TimeUnit.MILLISECONDS);
  new Thread(()->receive(address,pair,generation),"phone-subtitles").start();
 }
 private void receive(String address,String pair,int generation){int retry=250,failedConnections=0;boolean searched=false;
  while(!destroyed&&generation==session){HttpURLConnection conn=null;
   try{
    conn=(HttpURLConnection)new URL("http://"+address+":8787/events"+(pair==null?"":"?code="+pair+"&receiver="+receiverId)).openConnection();connection=conn;conn.setConnectTimeout(4000);conn.setReadTimeout(45000);conn.setRequestProperty("Accept","text/event-stream");
    int result=conn.getResponseCode();if(result==403){post(generation,()->{status.setText("رمز الربط غير صحيح؛ حدّثه من إعدادات الربط.");connectionPanel.setVisibility(View.VISIBLE);});return;}
    if(result!=200)throw new IOException("HTTP "+result);retry=250;failedConnections=0;post(generation,()->{status.setText("✓ متصل. الترجمة تظهر فوق اللعبة.");connectionPanel.setVisibility(View.VISIBLE);});
    try(BufferedReader lines=new BufferedReader(new InputStreamReader(conn.getInputStream(),StandardCharsets.UTF_8))){String event="",data="",line;
     while(!destroyed&&generation==session&&(line=lines.readLine())!=null){
      if(line.startsWith("event: "))event=line.substring(7);else if(line.startsWith("data: "))data=line.substring(6);else if(line.isEmpty()){
       if("subtitle".equals(event)){JSONObject json=new JSONObject(data);post(generation,()->applyCaption(json,"بث لحظي"));}
       else if("style".equals(event)){JSONObject style=new JSONObject(data);post(generation,()->{sourcePlacement="cover".equals(style.optString("placement"));captionPositionKey="";});}
       else if("settings".equals(event)){post(generation,this::showPanel);}
       else if("clear".equals(event)){JSONObject json=new JSONObject(data);post(generation,()->applyClear(json.optLong("revision",0)));}event="";data="";
      }
     }
    }
    if(!destroyed&&generation==session)throw new EOFException("Relay closed");
   }catch(Exception e){if(pair==null&&!searched&&++failedConnections>=3){searched=true;post(generation,this::discoverDesktop);}String reason=e instanceof SocketTimeoutException?"مهلة استقبال":e instanceof EOFException?"الجوال أغلق الاتصال":e instanceof ConnectException?"خدمة الجوال غير متاحة":e.getClass().getSimpleName();int count=++drops;post(generation,()->status.setText("إعادة ربط ("+count+"): "+reason));}
   finally{if(conn!=null)conn.disconnect();}if(destroyed||generation!=session)return;try{Thread.sleep(retry);}catch(InterruptedException e){return;}retry=Math.min(2500,retry*2);
  }
 }
 private void pollLatest(String address,String pair,int generation){
  if(destroyed||session!=generation)return;HttpURLConnection c=null;
  try{c=(HttpURLConnection)new URL("http://"+address+":8787/latest?code="+pair+"&receiver="+receiverId).openConnection();pollingConnection=c;c.setConnectTimeout(1800);c.setReadTimeout(1800);c.setUseCaches(false);if(c.getResponseCode()!=200)return;
   StringBuilder body=new StringBuilder();try(BufferedReader r=new BufferedReader(new InputStreamReader(c.getInputStream(),StandardCharsets.UTF_8))){String line;while((line=r.readLine())!=null){body.append(line);if(body.length()>20000)return;}}
   JSONObject json=new JSONObject(body.toString());post(generation,()->{if(json.optBoolean("active"))applyCaption(json,"استقبال احتياطي");else applyClear(json.optLong("revision",0));});
  }catch(Exception ignored){}finally{if(c!=null)c.disconnect();}
 }
 private void applyCaption(JSONObject json,String route){
  String text=json.optString("text","");long rev=json.optLong("revision",0);if(text.isEmpty()||rev>0&&rev<acceptedRevision)return;if(rev>0&&rev==acceptedRevision&&text.equals(lastArabic))return;
  acceptedRevision=Math.max(acceptedRevision,rev);manualTestUntil=0;if(!text.equals(lastArabic)){subtitleCount++;lastArabic=text;}positionCaption(json.optJSONObject("box"));String receipt="سطور مستلمة: "+subtitleCount+" · "+route;if(!android.text.TextUtils.equals(received.getText(),receipt))received.setText(receipt);show(text,Math.max(250,Math.min(10000,json.optInt("duration",5000))));
 }
 private void positionCaption(JSONObject box){
  FrameLayout.LayoutParams p=(FrameLayout.LayoutParams)subtitle.getLayoutParams();int w=getResources().getDisplayMetrics().widthPixels,h=getResources().getDisplayMetrics().heightPixels;
  double l=box==null?-1:box.optDouble("left",-1),t=box==null?-1:box.optDouble("top",-1),r=box==null?-1:box.optDouble("right",-1),b=box==null?-1:box.optDouble("bottom",-1);
  int mode=getPreferences(0).getInt("captionPlacement",0);String key=sourcePlacement+":"+mode+":"+l+":"+t+":"+r+":"+b+":"+w+":"+h+":"+getPreferences(0).getInt("subtitleBottom",7);if(key.equals(captionPositionKey))return;captionPositionKey=key;boolean valid=Double.isFinite(l)&&Double.isFinite(t)&&Double.isFinite(r)&&Double.isFinite(b)&&l>=0&&t>=0&&r<=1&&b<=1&&r>l&&b>t;
  if(valid&&mode!=1&&(mode==2||sourcePlacement||t<.55)){
   p.gravity=Gravity.TOP|Gravity.LEFT;p.leftMargin=(int)(l*w);p.topMargin=(int)(t*h);p.bottomMargin=0;p.width=Math.max(dp(110),(int)((r-l)*w));p.width=Math.min(p.width,w-p.leftMargin);subtitle.setMaxWidth(p.width);subtitle.setMaxHeight(Math.max(dp(50),h-p.topMargin));subtitle.setGravity(Gravity.TOP|Gravity.RIGHT);
  }else{p.gravity=Gravity.BOTTOM|Gravity.CENTER_HORIZONTAL;p.leftMargin=0;p.topMargin=0;p.width=-2;p.bottomMargin=Math.round(h*Math.max(2,Math.min(35,getPreferences(0).getInt("subtitleBottom",7)))/100f);subtitle.setMaxWidth((int)(w*.84));subtitle.setMaxHeight(h);subtitle.setGravity(Gravity.CENTER);}
  subtitle.setLayoutParams(p);
 }
 private void applyClear(long revision){if(revision>0&&revision<acceptedRevision)return;acceptedRevision=Math.max(acceptedRevision,revision);if(SystemClock.uptimeMillis()<manualTestUntil)return;lastArabic="";ui.removeCallbacks(hide);hide.run();}
 private void testLocal(String text){manualTestUntil=SystemClock.uptimeMillis()+7000;show(text,7000);}
 private void post(int generation,Runnable action){ui.post(()->{if(!destroyed&&session==generation)action.run();});}
 private void show(String text,int ms){if(!android.text.TextUtils.equals(subtitle.getText(),text))subtitle.setText(text);if(subtitle.getVisibility()!=View.VISIBLE)subtitle.setVisibility(View.VISIBLE);ui.removeCallbacks(hide);ui.postDelayed(hide,ms);}
 private void applySubtitleStyle(){
  int size=Math.max(14,Math.min(34,getPreferences(0).getInt("subtitleSize",20)));
  int opacity=Math.max(0,Math.min(100,getPreferences(0).getInt("subtitleOpacity",70)));
  subtitle.setTextSize(size);GradientDrawable background=new GradientDrawable();background.setColor((Math.round(opacity*255f/100)<<24));background.setCornerRadius(dp(6));subtitle.setBackground(background);
  if(subtitle.getLayoutParams() instanceof FrameLayout.LayoutParams){FrameLayout.LayoutParams params=(FrameLayout.LayoutParams)subtitle.getLayoutParams();params.bottomMargin=Math.round(getResources().getDisplayMetrics().heightPixels*Math.max(2,Math.min(35,getPreferences(0).getInt("subtitleBottom",7)))/100f);subtitle.setLayoutParams(params);}
 }
 private void subtitleSettings(){
  LinearLayout content=column();content.setPadding(dp(20),dp(10),dp(20),dp(10));
  TextView sizeLabel=label("",15),opacityLabel=label("",15);content.addView(sizeLabel);
  SeekBar size=new SeekBar(this);size.setTag("subtitle-size");size.setMax(20);size.setProgress(Math.max(0,Math.min(20,getPreferences(0).getInt("subtitleSize",20)-14)));content.addView(size);
  Spinner placement=new Spinner(this);placement.setAdapter(new ArrayAdapter<>(this,android.R.layout.simple_spinner_dropdown_item,new String[]{"تلقائي — القوائم قرب النص والحوار بالأسفل","الحوار دائمًا في الأسفل","الترجمة عند موضع النص المقروء"}));placement.setSelection(getPreferences(0).getInt("captionPlacement",0));placement.setOnItemSelectedListener(new android.widget.AdapterView.OnItemSelectedListener(){public void onNothingSelected(android.widget.AdapterView<?> parent){}public void onItemSelected(android.widget.AdapterView<?> parent,View view,int selected,long id){getPreferences(0).edit().putInt("captionPlacement",selected).apply();}});content.addView(placement);
  content.addView(opacityLabel);SeekBar opacity=new SeekBar(this);opacity.setMax(100);opacity.setProgress(Math.max(0,Math.min(100,getPreferences(0).getInt("subtitleOpacity",70))));content.addView(opacity);
  TextView positionLabel=label("",15);content.addView(positionLabel);SeekBar position=new SeekBar(this);position.setTag("subtitle-position");position.setMax(33);position.setProgress(Math.max(0,Math.min(33,getPreferences(0).getInt("subtitleBottom",7)-2)));content.addView(position);
  Runnable update=()->{positionLabel.setText("ارتفاع الترجمة من الأسفل: "+(position.getProgress()+2)+"%");sizeLabel.setText("حجم الخط: "+(size.getProgress()+14));opacityLabel.setText("خلفية الترجمة: "+opacity.getProgress()+"%");getPreferences(0).edit().putInt("subtitleSize",size.getProgress()+14).putInt("subtitleOpacity",opacity.getProgress()).putInt("subtitleBottom",position.getProgress()+2).apply();applySubtitleStyle();};
  SeekBar.OnSeekBarChangeListener listener=new SeekBar.OnSeekBarChangeListener(){public void onStartTrackingTouch(SeekBar bar){}public void onStopTrackingTouch(SeekBar bar){}public void onProgressChanged(SeekBar bar,int progress,boolean user){update.run();}};
  size.setOnSeekBarChangeListener(listener);opacity.setOnSeekBarChangeListener(listener);position.setOnSeekBarChangeListener(listener);update.run();
  new android.app.AlertDialog.Builder(this).setTitle("حجم وخلفية الترجمة").setView(content).setPositiveButton("تم",(d,w)->{}).show();
  testLocal("آرثر: معاينة حجم الترجمة العربية.");
 }
 private void repairDisplay(){
  getPreferences(0).edit().putInt("subtitleSize",20).putInt("subtitleOpacity",70).putInt("subtitleBottom",7).apply();
  applySubtitleStyle();subtitle.bringToFront();panelWindow.bringToFront();
  testLocal("اختبار الإصلاح — الترجمة العربية تظهر هنا");
  if(Pairing.privateIpv4(host.getText().toString().trim())&&(desktop.isChecked()||Pairing.validCode(code.getText().toString().trim())))connect();else discoverDesktop();
 }
 private void hidePanel(){if(tunedInput==null){showPanel();return;}panelWindow.setVisibility(View.GONE);menuHandle.setVisibility(View.GONE);View root=(View)panelWindow.getParent();root.setFocusableInTouchMode(true);root.setDefaultFocusHighlightEnabled(false);root.requestFocus();}
 private void showPanel(){panelWindow.setVisibility(View.VISIBLE);panelWindow.bringToFront();if(menuHandle!=null)menuHandle.setVisibility(View.GONE);panelWindow.setDescendantFocusability(ViewGroup.FOCUS_AFTER_DESCENDANTS);for(int n=0;n<panel.getChildCount();n++){View v=panel.getChildAt(n);if(v instanceof Button&&v.getVisibility()==View.VISIBLE){v.requestFocus();break;}}}
 @Override public boolean dispatchKeyEvent(KeyEvent event){
  int key=event.getKeyCode();if(key==swallowedKey){if(event.getAction()==KeyEvent.ACTION_UP)swallowedKey=-1;return true;}
  boolean hidden=panelWindow!=null&&panelWindow.getVisibility()!=View.VISIBLE;
  boolean shortcut=key==KeyEvent.KEYCODE_BACK||key==KeyEvent.KEYCODE_MENU||key==KeyEvent.KEYCODE_INFO||key==KeyEvent.KEYCODE_PROG_RED||hidden&&(key==KeyEvent.KEYCODE_DPAD_CENTER||key==KeyEvent.KEYCODE_ENTER||key==KeyEvent.KEYCODE_DPAD_UP||key==KeyEvent.KEYCODE_DPAD_DOWN||key==KeyEvent.KEYCODE_DPAD_LEFT||key==KeyEvent.KEYCODE_DPAD_RIGHT);
  if(shortcut){if(event.getAction()==KeyEvent.ACTION_DOWN){swallowedKey=key;if(event.getRepeatCount()==0)onKeyDown(key,event);}return true;}
  return super.dispatchKeyEvent(event);
 }
 @Override public boolean onKeyDown(int key,KeyEvent event){
  if(key==KeyEvent.KEYCODE_PROG_RED){testLocal("اختبار العربية — الحروف متصلة؟");return true;}
  if(key==KeyEvent.KEYCODE_BACK||key==KeyEvent.KEYCODE_MENU){if(panelWindow.getVisibility()==View.VISIBLE)hidePanel();else showPanel();return true;}
  if(key==KeyEvent.KEYCODE_INFO){showPanel();return true;}
  if(panelWindow.getVisibility()!=View.VISIBLE&&(key==KeyEvent.KEYCODE_DPAD_CENTER||key==KeyEvent.KEYCODE_ENTER||key==KeyEvent.KEYCODE_DPAD_UP||key==KeyEvent.KEYCODE_DPAD_DOWN||key==KeyEvent.KEYCODE_DPAD_LEFT||key==KeyEvent.KEYCODE_DPAD_RIGHT)){showPanel();return true;}
  return super.onKeyDown(key,event);
 }
 @Override public void onBackPressed(){if(panelWindow.getVisibility()==View.VISIBLE)hidePanel();else showPanel();}
 @Override protected void onResume(){super.onResume();resumed=true;resumeTicket++;if(input!=null)refreshInputs();
  trace("resume; session="+(tunedInput!=null));if(!playRequested||tunedInput==null)showPanel();if(tunedInput!=null)restoreAudio();
 }
 @Override protected void onPause(){trace("pause; session="+(tunedInput!=null));resumed=false;resumeTicket++;if(tv!=null)tv.setStreamVolume(0f);if(audioManager!=null&&audioFocus!=null)audioManager.abandonAudioFocusRequest(audioFocus);super.onPause();}
 private void releaseHdmiSession(){videoAvailable=false;if(tunedInput!=null){restoreHdmi=true;tunedInput=null;}if(tv!=null)tv.reset();dev.arabicgames.update.UpdateActivity.setPlaySessionActive(false);}
 @Override protected void onStop(){trace("stop; retained session="+(tunedInput!=null));playRequested=false;resumeTicket++;if(tv!=null)tv.setStreamVolume(0f);if(audioManager!=null&&audioFocus!=null)audioManager.abandonAudioFocusRequest(audioFocus);super.onStop();}
 @Override public void onConfigurationChanged(android.content.res.Configuration config){super.onConfigurationChanged(config);trace("display configuration changed; retained session="+(tunedInput!=null));captionPositionKey="";applySubtitleStyle();}
 @Override public void onDestroy(){trace("destroy");if(inputManager!=null)inputManager.unregisterCallback(inputChanges);dev.arabicgames.update.UpdateActivity.setPlaySessionActive(false);destroyed=true;if(Build.VERSION.SDK_INT>=33&&backCallback!=null)getOnBackInvokedDispatcher().unregisterOnBackInvokedCallback(backCallback);pairingAttempt++;if(pollTask!=null)pollTask.cancel(true);poller.shutdownNow();if(pollingConnection!=null)pollingConnection.disconnect();session++;if(connection!=null)connection.disconnect();ui.removeCallbacksAndMessages(null);tv.reset();if(audioManager!=null)audioManager.abandonAudioFocusRequest(audioFocus);super.onDestroy();}
 private int dp(int n){return Math.round(n*getResources().getDisplayMetrics().density);}
 private TextView label(String text,int size){TextView v=new TextView(this);v.setText(text);v.setTextColor(Color.WHITE);v.setTextSize(size);v.setPadding(0,dp(3),0,dp(3));return v;}
 private LinearLayout column(){LinearLayout v=new LinearLayout(this);v.setOrientation(LinearLayout.VERTICAL);v.setLayoutDirection(View.LAYOUT_DIRECTION_RTL);return v;}
 private Button button(LinearLayout parent,String text,Runnable action){Button b=new Button(this);b.setText(text);b.setTextSize(13);b.setAllCaps(false);b.setMinHeight(dp(44));b.setPadding(dp(10),dp(6),dp(10),dp(6));
  android.graphics.drawable.StateListDrawable colors=new android.graphics.drawable.StateListDrawable();
  GradientDrawable focused=new GradientDrawable();focused.setColor(0xff72e2b1);focused.setCornerRadius(dp(10));
  GradientDrawable normal=new GradientDrawable();normal.setColor(0xff34465f);normal.setCornerRadius(dp(10));
  colors.addState(new int[]{android.R.attr.state_focused},focused);colors.addState(new int[]{},normal);b.setBackground(colors);
  b.setTextColor(new android.content.res.ColorStateList(new int[][]{new int[]{android.R.attr.state_focused},new int[]{}},new int[]{0xff0f1724,0xfff4f7fa}));
  b.setOnClickListener(v->action.run());LinearLayout.LayoutParams layout=new LinearLayout.LayoutParams(-1,-2);layout.topMargin=dp(6);parent.addView(b,layout);return b;}
 private final class PanelScroll extends ScrollView {PanelScroll(Context c){super(c);}@Override protected void onMeasure(int w,int h){super.onMeasure(w,View.MeasureSpec.makeMeasureSpec((int)(getResources().getDisplayMetrics().heightPixels*.76),View.MeasureSpec.AT_MOST));}}
}
