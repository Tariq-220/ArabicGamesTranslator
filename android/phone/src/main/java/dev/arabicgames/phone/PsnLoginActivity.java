// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames.phone;
import android.app.*;
import android.os.*;
import android.view.*;
import android.webkit.*;
import android.net.http.SslError;
import android.widget.*;
import java.util.concurrent.*;
import java.util.concurrent.atomic.AtomicBoolean;

/** Sony owns the password UI. The app receives only the OAuth redirect and stores the account ID. */
public final class PsnLoginActivity extends Activity {
 private WebView browser;private TextView status;private String state;
 private final AtomicBoolean exchanging=new AtomicBoolean();private final ExecutorService worker=Executors.newSingleThreadExecutor();
 private volatile boolean closed;
 @Override public void onCreate(Bundle saved){super.onCreate(saved);getWindow().addFlags(WindowManager.LayoutParams.FLAG_SECURE);state=PsnProtocol.nonce();
  LinearLayout root=Ui.column(this);root.setBackgroundColor(Ui.BG);root.setPadding(Ui.dp(this,12),Ui.dp(this,12),Ui.dp(this,12),Ui.dp(this,12));
  root.addView(Ui.text(this,"Sony · ادخل بالحساب الثانوي",21));status=Ui.text(this,"نفس المستخدم الثانوي الموجود على PS5. كلمة المرور تُكتب في صفحة Sony فقط.",13);root.addView(status);
  browser=new WebView(this);WebView.setWebContentsDebuggingEnabled(false);WebSettings settings=browser.getSettings();settings.setJavaScriptEnabled(true);settings.setDomStorageEnabled(true);settings.setAllowFileAccess(false);settings.setAllowContentAccess(false);settings.setMixedContentMode(WebSettings.MIXED_CONTENT_NEVER_ALLOW);
  CookieManager.getInstance().setAcceptThirdPartyCookies(browser,true);
  browser.setWebViewClient(new WebViewClient(){
   @Override public boolean shouldOverrideUrlLoading(WebView view,WebResourceRequest request){return request.isForMainFrame()&&navigate(request.getUrl().toString());}
   @Override public boolean shouldOverrideUrlLoading(WebView view,String url){return navigate(url);}
   @Override public void onReceivedSslError(WebView v,SslErrorHandler handler,SslError e){handler.cancel();status.setText("تعذر التحقق من اتصال Sony الآمن؛ لم تُقبل الشهادة.");}
  });
  root.addView(browser,new LinearLayout.LayoutParams(-1,0,1));Ui.button(this,root,"إلغاء والرجوع",false,this::finish);PhoneInsets.fit(root);setContentView(root);browser.loadUrl(PsnProtocol.authorize(state));
 }
 private boolean navigate(String url){
  if(PsnProtocol.redirect(url)){
   final String code;try{code=PsnProtocol.code(url,state);}catch(Exception e){status.setText("لم يكتمل تسجيل Sony أو لم تطابق الاستجابة جلسة الدخول. ارجع وأعد تسجيل الثانوي.");return true;}
   if(!exchanging.compareAndSet(false,true))return true;browser.setVisibility(View.GONE);status.setText("جارٍ التحقق من Sony وحفظ معرّف الحساب بشكل مشفّر…");
   worker.execute(()->{try{byte[] account=PsnAuth.account(code);if(closed)return;Ps5Store.account(this,account);runOnUiThread(()->{if(!closed){setResult(RESULT_OK);finish();}});}catch(Exception e){runOnUiThread(()->{if(!closed)status.setText(e instanceof PsnAuth.Failure?"Sony لم يُكمل الدخول (HTTP "+((PsnAuth.Failure)e).status+"). ارجع وأعد المحاولة.":"تعذر إكمال الاتصال أو التخزين الآمن. لم نحفظ كلمة المرور؛ ارجع وحاول مجددًا.");});}});return true;
  }
  if(!PsnProtocol.sonyPage(url)){status.setText("تم إيقاف انتقال خارج صفحة Sony الآمنة.");return true;}return false;
 }
 @Override public void onDestroy(){closed=true;worker.shutdownNow();if(browser!=null){browser.stopLoading();browser.destroy();}super.onDestroy();}
}
