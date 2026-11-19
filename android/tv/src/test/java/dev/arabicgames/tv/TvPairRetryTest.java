package dev.arabicgames.tv;
import android.os.Looper;import android.widget.EditText;import java.net.*;import java.io.*;import java.nio.charset.StandardCharsets;import java.util.concurrent.atomic.*;import org.junit.*;import org.junit.runner.RunWith;import org.robolectric.*;import org.robolectric.annotation.Config;import org.robolectric.util.ReflectionHelpers;import static org.junit.Assert.*;
@RunWith(RobolectricTestRunner.class) @Config(sdk=35)
public class TvPairRetryTest {
 @Test public void TemporaryBusyResponseDoesNotAbortPairing()throws Exception{
  ServerSocket server=new ServerSocket(8787);AtomicReference<Throwable> failure=new AtomicReference<>();AtomicInteger requests=new AtomicInteger();
  Thread responder=new Thread(()->{try{for(int n=0;n<2;n++)try(Socket peer=server.accept()){
   peer.setSoTimeout(4000);BufferedReader in=new BufferedReader(new InputStreamReader(peer.getInputStream(),StandardCharsets.UTF_8));String first=in.readLine();assertTrue(first.contains("/pair?ticket="));String line;while((line=in.readLine())!=null&&!line.isEmpty()){}
   requests.incrementAndGet();byte[] body=(n==0?"{}":"{\"code\":\"12345678\"}").getBytes(StandardCharsets.UTF_8);peer.getOutputStream().write(((n==0?"HTTP/1.1 503 Busy":"HTTP/1.1 200 OK")+"\r\nContent-Type: application/json\r\nContent-Length: "+body.length+"\r\nConnection: close\r\n\r\n").getBytes(StandardCharsets.US_ASCII));peer.getOutputStream().write(body);peer.getOutputStream().flush();
  }}catch(Throwable e){failure.set(e);}},"pair-retry-test");responder.start();
  try(var activity=Robolectric.buildActivity(MainActivity.class).setup()){
   MainActivity a=activity.get();ReflectionHelpers.callInstanceMethod(a,"pairPhone",ReflectionHelpers.ClassParameter.from(String.class,"127.0.0.1"));EditText code=ReflectionHelpers.getField(a,"code");
   for(int n=0;n<200&&!"12345678".equals(code.getText().toString());n++){Shadows.shadowOf(Looper.getMainLooper()).idle();Thread.sleep(20);}
   assertNull(failure.get());assertEquals(2,requests.get());assertEquals("12345678",code.getText().toString());
  }finally{server.close();responder.join(5000);}
 }
}
