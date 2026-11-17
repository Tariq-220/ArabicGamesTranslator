// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames.tv;
import android.view.*;
import android.widget.*;
import java.net.*;
import java.io.*;
import java.nio.charset.StandardCharsets;
import java.lang.reflect.*;
import java.util.concurrent.atomic.AtomicReference;
import org.junit.Test;
import org.junit.runner.RunWith;
import org.robolectric.*;
import org.robolectric.annotation.*;
import static org.junit.Assert.*;
@RunWith(RobolectricTestRunner.class)
@GraphicsMode(GraphicsMode.Mode.NATIVE)
@Config(sdk=35,qualifiers="w960dp-h540dp-land-television")
public class TvDeliveryTest {
 @Test public void actualHttpSubtitleReachesTheVisibleTvCaption()throws Exception{
  String arabic="وصلت الترجمة العربية من الجوال.";
  ServerSocket server=new ServerSocket(8787);AtomicReference<Throwable> failure=new AtomicReference<>();
  Thread relay=new Thread(()->{try(Socket peer=server.accept()){
   peer.setSoTimeout(5000);BufferedReader request=new BufferedReader(new InputStreamReader(peer.getInputStream(),StandardCharsets.UTF_8));String line;while((line=request.readLine())!=null&&!line.isEmpty()){}
   byte[] body=("event: subtitle\ndata: {\"text\":\""+arabic+"\",\"duration\":5000}\n\n").getBytes(StandardCharsets.UTF_8);
   OutputStream out=peer.getOutputStream();out.write(("HTTP/1.1 200 OK\r\nContent-Type: text/event-stream; charset=utf-8\r\nConnection: close\r\nContent-Length: "+body.length+"\r\n\r\n").getBytes(StandardCharsets.US_ASCII));out.write(body);out.flush();
  }catch(Throwable t){failure.set(t);}},"test-relay");relay.start();
  Thread receiver=null;
  try(var activity=Robolectric.buildActivity(MainActivity.class).setup()){
   MainActivity a=activity.get();Method receive=MainActivity.class.getDeclaredMethod("receive",String.class,String.class,int.class);receive.setAccessible(true);
   receiver=new Thread(()->{try{receive.invoke(a,"127.0.0.1","01234567",0);}catch(Throwable t){failure.set(t);}},"test-tv-receiver");receiver.start();
   Field field=MainActivity.class.getDeclaredField("subtitle");field.setAccessible(true);TextView caption=(TextView)field.get(a);
   for(int i=0;i<150&&!arabic.equals(caption.getText().toString());i++){Shadows.shadowOf(android.os.Looper.getMainLooper()).idle();Thread.sleep(20);}
   assertNull(failure.get());assertEquals(arabic,caption.getText().toString());assertEquals(View.VISIBLE,caption.getVisibility());
   Field count=MainActivity.class.getDeclaredField("subtitleCount");count.setAccessible(true);assertEquals(1,count.getInt(a));
  }finally{server.close();relay.join(2000);if(receiver!=null)receiver.join(5000);}
 }
}
