// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames.tv;
import android.view.*;
import android.widget.*;
import org.junit.Test;
import org.junit.runner.RunWith;
import org.robolectric.*;
import org.robolectric.annotation.*;
import org.json.JSONObject;
import static org.junit.Assert.*;
import java.net.*;
import java.io.*;
import java.nio.charset.StandardCharsets;
import java.lang.reflect.*;
@RunWith(RobolectricTestRunner.class) @GraphicsMode(GraphicsMode.Mode.NATIVE)
@Config(sdk=35,qualifiers="w960dp-h540dp-land-television")
public class TvFallbackTest {
 @Test public void fallbackDisplaysArabicAndRejectsOutOfOrderUpdates()throws Exception{
  String text="العربية وصلت بالمسار الاحتياطي";ServerSocket server=new ServerSocket(0);final String endpoint=TestLanAddress.endpoint(server.getLocalPort());
  Thread stub=new Thread(()->{try{boolean delivered=false;while(!delivered){try(Socket peer=server.accept()){BufferedReader in=new BufferedReader(new InputStreamReader(peer.getInputStream(),StandardCharsets.UTF_8));String request=in.readLine(),line;while((line=in.readLine())!=null&&!line.isEmpty()){}OutputStream out=peer.getOutputStream();if(request==null||!request.startsWith("GET /latest?")){out.write("HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\nConnection: close\r\n\r\n".getBytes(StandardCharsets.US_ASCII));out.flush();continue;}
   byte[] body=("{\"active\":true,\"revision\":5,\"text\":\""+text+"\",\"duration\":5000}").getBytes(StandardCharsets.UTF_8);out.write(("HTTP/1.1 200 OK\r\nContent-Type: application/json; charset=utf-8\r\nContent-Length: "+body.length+"\r\nConnection: close\r\n\r\n").getBytes(StandardCharsets.US_ASCII));out.write(body);out.flush();delivered=true;}}}catch(IOException e){if(!server.isClosed())throw new RuntimeException(e);}},"snapshot-test");stub.start();
  try(var c=Robolectric.buildActivity(MainActivity.class).setup()){
   MainActivity a=c.get();Method poll=MainActivity.class.getDeclaredMethod("pollLatest",String.class,String.class,int.class);poll.setAccessible(true);Thread reader=new Thread(()->{try{poll.invoke(a,endpoint,"01234567",0);}catch(Exception e){throw new RuntimeException(e);}});reader.start();reader.join(4000);Shadows.shadowOf(android.os.Looper.getMainLooper()).idle();
   Field field=MainActivity.class.getDeclaredField("subtitle");field.setAccessible(true);TextView caption=(TextView)field.get(a);assertEquals(text,caption.getText().toString());assertEquals(View.VISIBLE,caption.getVisibility());
   Method apply=MainActivity.class.getDeclaredMethod("applyCaption",JSONObject.class,String.class);apply.setAccessible(true);apply.invoke(a,new JSONObject().put("revision",4).put("text","سطر قديم").put("duration",5000),"test");assertEquals(text,caption.getText().toString());
   Method clear=MainActivity.class.getDeclaredMethod("applyClear",long.class);clear.setAccessible(true);clear.invoke(a,3L);assertEquals(View.VISIBLE,caption.getVisibility());clear.invoke(a,6L);assertEquals(View.INVISIBLE,caption.getVisibility());
  }finally{server.close();stub.join(2000);}
 }
}
