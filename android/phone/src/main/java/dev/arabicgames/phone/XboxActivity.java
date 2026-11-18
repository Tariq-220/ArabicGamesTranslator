// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames.phone;
import android.app.*;
import android.content.*;
import android.graphics.Color;
import android.net.Uri;
import android.os.*;
import android.util.Base64;
import android.view.*;
import android.webkit.*;
import android.widget.*;
import org.json.*;
import java.nio.charset.StandardCharsets;
import java.io.*;

/** The official Xbox page stays intact; translation and local audio controls are separate. */
public final class XboxActivity extends Activity {
 private WebView browser;private TextView status,caption;private Button startButton,stopButton,soundButton;
 private ProximityDisplay proximity;
 private View darkCover;private float previousBrightness;private int previousSystemUi;
 private String script,audioScript;private boolean phoneMuted=true;
 private final dev.arabicgames.ReadingSession reading=new dev.arabicgames.ReadingSession();
 private boolean pending,resumed,destroyed;private int requestId;private long audioAt,pendingAt;
 private final Handler ui=new Handler(Looper.getMainLooper());
 private final Runnable poll=new Runnable(){public void run(){
  if(destroyed||!resumed)return;
  if(pending&&SystemClock.elapsedRealtime()-pendingAt>=3000){pending=false;requestId++;PipelineMetrics.captureTimeouts.incrementAndGet();Status.message="تأخرت قراءة فيديو Xbox؛ أُعيد طلب إطار حديث.";}
  String connection=Status.clients>0?"شاشات متصلة: "+Status.clients:"لا توجد شاشة متصلة";
  status.setText(connection+" · "+(reading.requested()?CaptureService.directState():"اضغط بدء الترجمة")+"\n"+Status.message);
  String arabic=Status.arabic;
  if(!caption.getText().toString().equals(arabic))caption.setText(arabic);
  caption.setVisibility(arabic.isEmpty()?View.GONE:View.VISIBLE);
  if(SystemClock.elapsedRealtime()-audioAt>700){applyAudio();audioAt=SystemClock.elapsedRealtime();}
  if(reading.active()&&!pending&&CaptureService.acceptsDirect()&&isXbox(browser.getUrl())){
   pending=true;pendingAt=SystemClock.elapsedRealtime();final int callbackId=++requestId,generation=reading.generation();browser.evaluateJavascript("window.__arabicRegion="+JSONObject.quote(getSharedPreferences("reading",0).getString("region","subtitles"))+";\n"+script,value->{
    if(callbackId!=requestId)return;pending=false;if(!reading.accepts(generation)||destroyed)return;
    try{
     Object raw=new JSONTokener(value).nextValue();if(!(raw instanceof String))return;JSONObject frame=new JSONObject((String)raw);
     switch(frame.getString("status")){
      case "FRAME":String encoded=frame.getString("image");int comma=encoded.indexOf(',');if(comma>0&&encoded.length()<800000)CaptureService.directJpeg(Base64.decode(encoded.substring(comma+1),Base64.DEFAULT));break;
      case "CAPTURE_BLOCKED":Status.message="المشغّل يمنع قراءة الفيديو. جرّب مشاركة الشاشة إذا سمح بها النظام.";reading.stop();CaptureService.pauseDirect();updateButtons();break;
      case "WAIT_VIDEO":Status.message="بانتظار فيديو Xbox. اختر جهازك وشغّل اللعبة داخل الصفحة.";break;
     }
    }catch(Exception e){Status.message="لم تصل صورة فيديو صالحة. افتح تفاصيل الترجمة من القائمة.";}
   });
  }
  ui.postDelayed(this,(AiPreferences.NATIVE.equals(AiPreferences.provider(XboxActivity.this))||AiPreferences.LOCAL.equals(AiPreferences.provider(XboxActivity.this)))?LocalTiming.interval(XboxActivity.this):200);
 }};
 private static boolean isXbox(String url){try{Uri u=Uri.parse(url);String h=u.getHost();return "https".equals(u.getScheme())&&h!=null&&(h.equals("xbox.com")||h.endsWith(".xbox.com"));}catch(Exception e){return false;}}
 @Override public void onCreate(Bundle saved){super.onCreate(saved);getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);getWindow().setStatusBarColor(Ui.BG);getWindow().setNavigationBarColor(Ui.BG);
  proximity=new ProximityDisplay(this);
  phoneMuted=getPreferences(0).getBoolean("phoneMuted",true);
  LinearLayout root=Ui.column(this);root.setBackgroundColor(Ui.BG);root.setPadding(Ui.dp(this,10),Ui.dp(this,4),Ui.dp(this,10),Ui.dp(this,6));
  LinearLayout heading=new LinearLayout(this);heading.setGravity(Gravity.CENTER_VERTICAL);TextView title=Ui.text(this,"Xbox · بدون كاميرا",16);heading.addView(title,new LinearLayout.LayoutParams(0,-2,1));
  Button display=new Button(this);display.setText("الشاشة");display.setMinHeight(Ui.dp(this,48));display.setOnClickListener(v->displayOptions());heading.addView(display,new LinearLayout.LayoutParams(Ui.dp(this,88),-2));
  Button menu=new Button(this);menu.setText("⋮");menu.setTextSize(22);menu.setContentDescription("خيارات الجلسة");menu.setOnClickListener(v->menu(menu));heading.addView(menu,new LinearLayout.LayoutParams(Ui.dp(this,48),Ui.dp(this,40)));root.addView(heading);
  LinearLayout controls=new LinearLayout(this);controls.setLayoutDirection(View.LAYOUT_DIRECTION_RTL);controls.setOrientation(LinearLayout.HORIZONTAL);
  startButton=Ui.button(this,controls,"بدء الترجمة",true,()->{reading.start();updateButtons();Status.message="جارٍ قراءة فيديو Xbox…";});weight(startButton,1);
  stopButton=Ui.button(this,controls,"إيقاف",false,()->{reading.stop();requestId++;pending=false;CaptureService.pauseDirect();updateButtons();Status.message="الترجمة متوقفة بطلبك.";});weight(stopButton,.65f);
  soundButton=Ui.button(this,controls,"",false,()->{phoneMuted=!phoneMuted;getPreferences(0).edit().putBoolean("phoneMuted",phoneMuted).apply();applyAudio();updateButtons();});weight(soundButton,1.4f);root.addView(controls);updateButtons();
  status=Ui.text(this,"اختر جهاز Xbox، ثم اضغط بدء الترجمة بعد ظهور اللعبة.",12);status.setMaxLines(3);root.addView(status);
  FrameLayout video=new FrameLayout(this);root.addView(video,new LinearLayout.LayoutParams(-1,0,1));
  browser=new WebView(this);WebSettings settings=browser.getSettings();settings.setJavaScriptEnabled(true);settings.setDomStorageEnabled(true);settings.setMediaPlaybackRequiresUserGesture(false);settings.setAllowFileAccess(false);settings.setAllowContentAccess(false);settings.setMixedContentMode(WebSettings.MIXED_CONTENT_NEVER_ALLOW);settings.setSafeBrowsingEnabled(true);
  CookieManager.getInstance().setAcceptCookie(true);CookieManager.getInstance().setAcceptThirdPartyCookies(browser,true);
  browser.setWebViewClient(new WebViewClient(){
   @Override public boolean shouldOverrideUrlLoading(WebView view,WebResourceRequest request){return !"https".equals(request.getUrl().getScheme());}
   @Override public void onPageFinished(WebView view,String url){if(!isXbox(url))Status.message="أكمل الدخول في صفحة Microsoft الرسمية.";else applyAudio();}
  });browser.setWebChromeClient(new WebChromeClient(){@Override public void onPermissionRequest(PermissionRequest request){request.deny();}});
  video.addView(browser,new FrameLayout.LayoutParams(-1,-1));
  caption=Ui.text(this,"",19);caption.setGravity(Gravity.CENTER);caption.setTextDirection(View.TEXT_DIRECTION_RTL);caption.setPadding(Ui.dp(this,12),Ui.dp(this,8),Ui.dp(this,12),Ui.dp(this,8));caption.setBackground(Ui.background(this,0xee0b111c));caption.setVisibility(View.GONE);
  FrameLayout.LayoutParams cp=new FrameLayout.LayoutParams(-1,-2,Gravity.BOTTOM);cp.setMargins(Ui.dp(this,8),0,Ui.dp(this,8),Ui.dp(this,8));video.addView(caption,cp);
  PhoneInsets.fit(root);setContentView(root);
  try{script=asset("xbox-frame.js");audioScript=asset("phone-audio.js");}catch(Exception e){Status.message="تعذر تحميل قارئ الفيديو.";finish();return;}
  startForegroundService(new Intent(this,CaptureService.class).setAction("direct"));browser.loadUrl("https://www.xbox.com/remoteplay");
 }
 private String asset(String name)throws IOException{try(InputStream in=getAssets().open(name)){ByteArrayOutputStream out=new ByteArrayOutputStream();byte[] b=new byte[4096];int n;while((n=in.read(b))!=-1)out.write(b,0,n);return out.toString(StandardCharsets.UTF_8.name());}}
 private void applyAudio(){if(browser!=null&&audioScript!=null&&isXbox(browser.getUrl()))browser.evaluateJavascript("window.__arabicPhoneMuted="+phoneMuted+";\n"+audioScript,null);}
 private void menu(View anchor){PopupMenu menu=new PopupMenu(this,anchor);
  menu.getMenu().add("إطفاء بحساس القرب / حماية OLED").setOnMenuItemClickListener(item->{displayOptions();return true;});
  menu.getMenu().add("تعتيم OLED — ليس قفل الشاشة").setOnMenuItemClickListener(item->{enterDarkMode();return true;});
  android.view.MenuItem dialogueOnly=menu.getMenu().add("الحوار فقط — تجاهل قوائم Xbox وأسماء الطاقم المعروفة");dialogueOnly.setCheckable(true).setChecked(getSharedPreferences("reading",0).getBoolean("dialogueOnly",true));dialogueOnly.setOnMenuItemClickListener(item->{boolean on=!item.isChecked();getSharedPreferences("reading",0).edit().putBoolean("dialogueOnly",on).apply();CaptureService.readingChanged();Status.message=on?"تنقية الحوار مفعّلة؛ قوائم Xbox لا تُرسل للمحرك.":"تنقية الحوار متوقفة؛ اختر القوائم لترجمتها.";return true;});
  menu.getMenu().add("منطقة القراءة / القوائم / رسائل الهاتف").setOnMenuItemClickListener(item->{ReadingOptions.show(this);return true;});
  menu.getMenu().add("سجل الحوارات / تصدير حزمة").setOnMenuItemClickListener(item->{startActivity(new Intent(this,HistoryActivity.class));return true;});menu.getMenu().add("محرك الترجمة / AI").setOnMenuItemClickListener(item->{startActivity(new Intent(this,AiSettingsActivity.class));return true;});menu.getMenu().add("اختبار إرسال إلى الشاشة").setOnMenuItemClickListener(item->{reading.stop();requestId++;pending=false;updateButtons();if(!CaptureService.screenTest())Status.message="خدمة الربط غير جاهزة.";return true;});menu.getMenu().add("تفاصيل الترجمة والربط").setOnMenuItemClickListener(item->{new AlertDialog.Builder(this).setTitle("تفاصيل الترجمة").setMessage("IP: "+Status.address()+"\nالرمز: "+Status.code+"\nشاشات: "+Status.clients+" | صور مقروءة: "+Status.framesRead+"\n"+Status.metrics+"\n"+Status.cloudMetrics+"\nEnglish: "+Status.observedOcr+"\nالعربية: "+Status.arabic).setPositiveButton("إغلاق",null).show();return true;});
  menu.getMenu().add("مساعدة صوت الشاشة").setOnMenuItemClickListener(item->{new AlertDialog.Builder(this).setTitle("الصوت من الشاشة").setMessage("زر الصوت هنا يغيّر صوت فيديو الجوال فقط. في تطبيق الشاشة فعّل صوت HDMI.\nإذا كتم Xbox السماعات أثناء Remote Play، راجع Xbox: Settings > General > Volume & audio output > Additional options، وألغِ Mute speaker audio when headset attached إن كان مفعّلًا. لا يغيّر التطبيق مستوى صوت التلفزيون أو إعداد Xbox.").setPositiveButton("إغلاق",null).show();return true;});
  menu.getMenu().add("إنهاء جلسة Xbox والعودة").setOnMenuItemClickListener(item->{finish();return true;});menu.show();
 }
 private void weight(Button b,float value){LinearLayout.LayoutParams p=new LinearLayout.LayoutParams(0,-2,value);p.setMargins(Ui.dp(this,3),0,Ui.dp(this,3),0);b.setLayoutParams(p);b.setTextSize(13);}
 private void displayOptions(){
  String[] choices={proximity.active()?"إيقاف حساس القرب":"إطفاء الشاشة بحساس القرب — تجريبي","تعتيم OLED أسود — الشاشة تبقى مشغلة"};
  new AlertDialog.Builder(this).setTitle("شاشة الجوال أثناء الترجمة").setItems(choices,(dialog,which)->{
   if(which==1){proximity.stop();enterDarkMode();return;}
   if(proximity.active()){proximity.stop();Status.message="أُوقف وضع حساس القرب.";return;}
   if(!proximity.supported()){new AlertDialog.Builder(this).setMessage("النظام لا يعلن دعم إطفاء الشاشة بحساس القرب. يمكنك استخدام التعتيم الأسود.").setPositiveButton("حسنًا",null).show();return;}
   new AlertDialog.Builder(this).setTitle("تجربة حساس القرب").setMessage("ابدأ الترجمة أولًا. بعد التفعيل غطِّ حساس القرب أعلى الهاتف؛ النظام يطفئ اللوحة ويعيدها عند كشف الحساس، بدون قفل الجهاز. اختبر وصول ترجمة جديدة للتلفزيون لمدة دقيقة. استمرار فيديو Xbox أثناء الإطفاء يعتمد على جهازك. الخروج من التطبيق يلغي الوضع. لا تضغط زر القفل.")
    .setPositiveButton("تفعيل",(d,w)->{exitDarkMode();Status.message=proximity.start()?"حساس القرب مفعّل: غطِّ الحساس لإطفاء اللوحة؛ اكشفه لإعادتها.":"تعذر تفعيل حساس القرب على هذا الجهاز.";}).setNegativeButton("إلغاء",null).show();
  }).show();
 }
 void enterDarkMode(){
  if(darkCover!=null)return;
  new AlertDialog.Builder(this).setTitle("تعتيم الجوال أثناء Xbox").setMessage("تجريبي: يخفي الواجهة بخلفية سوداء ويخفض السطوع، لكنه لا يقفل الشاشة. قد يقلل عرض العناصر الثابتة على OLED؛ ليس ضمانًا ضد الاحتراق. جرّب أن الترجمة تستمر على الشاشة. اضغط مطولًا على السواد للعودة، أو رجوع. لا تستخدم زر قفل الجوال.")
   .setPositiveButton("تعتيم",(dialog,which)->{
    previousBrightness=getWindow().getAttributes().screenBrightness;previousSystemUi=getWindow().getDecorView().getSystemUiVisibility();
    darkCover=new View(this);darkCover.setBackgroundColor(Color.BLACK);darkCover.setContentDescription("اضغط مطولًا لإلغاء التعتيم");darkCover.setOnLongClickListener(view->{exitDarkMode();return true;});darkCover.setOnClickListener(view->{});
    ((android.view.ViewGroup)findViewById(android.R.id.content)).addView(darkCover,new android.view.ViewGroup.LayoutParams(-1,-1));
    WindowManager.LayoutParams attrs=getWindow().getAttributes();attrs.screenBrightness=.01f;getWindow().setAttributes(attrs);
    if(Build.VERSION.SDK_INT>=30){android.view.WindowInsetsController bars=getWindow().getInsetsController();if(bars!=null){bars.setSystemBarsBehavior(android.view.WindowInsetsController.BEHAVIOR_SHOW_TRANSIENT_BARS_BY_SWIPE);bars.hide(android.view.WindowInsets.Type.systemBars());}}
    else getWindow().getDecorView().setSystemUiVisibility(View.SYSTEM_UI_FLAG_FULLSCREEN|View.SYSTEM_UI_FLAG_HIDE_NAVIGATION|View.SYSTEM_UI_FLAG_IMMERSIVE_STICKY);
   }).setNegativeButton("إلغاء",null).show();
 }
 private void exitDarkMode(){if(darkCover==null)return;((android.view.ViewGroup)darkCover.getParent()).removeView(darkCover);darkCover=null;WindowManager.LayoutParams attrs=getWindow().getAttributes();attrs.screenBrightness=previousBrightness;getWindow().setAttributes(attrs);
  if(Build.VERSION.SDK_INT>=30){android.view.WindowInsetsController bars=getWindow().getInsetsController();if(bars!=null)bars.show(android.view.WindowInsets.Type.systemBars());}else getWindow().getDecorView().setSystemUiVisibility(previousSystemUi);
 }
 private void updateButtons(){if(startButton!=null)startButton.setEnabled(!reading.requested());if(stopButton!=null)stopButton.setEnabled(reading.requested());if(soundButton!=null)soundButton.setText(phoneMuted?"صوت الجوال: مكتوم":"صوت الجوال: مسموع");}
 @Override protected void onResume(){super.onResume();resumed=true;reading.resume();if(reading.requested())Status.message="استؤنفت قراءة الفيديو.";if(browser!=null)browser.onResume();ui.post(poll);}
 @Override protected void onPause(){if(proximity!=null)proximity.stop();resumed=false;reading.pause();requestId++;pending=false;CaptureService.pauseDirect();ui.removeCallbacks(poll);if(browser!=null)browser.onPause();super.onPause();}
 @Override public void onBackPressed(){if(darkCover!=null){exitDarkMode();return;}if(browser.canGoBack())browser.goBack();else super.onBackPressed();}
 @Override protected void onDestroy(){if(proximity!=null)proximity.stop();exitDarkMode();destroyed=true;ui.removeCallbacksAndMessages(null);CaptureService.pauseDirect();if(browser!=null){browser.stopLoading();browser.destroy();}super.onDestroy();}
}
