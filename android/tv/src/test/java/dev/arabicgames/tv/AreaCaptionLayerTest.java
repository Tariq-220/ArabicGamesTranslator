// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames.tv;
import android.os.Looper;
import android.view.View;
import android.widget.TextView;
import org.json.JSONObject;
import org.junit.Test;
import org.junit.runner.RunWith;
import org.robolectric.RobolectricTestRunner;
import org.robolectric.RuntimeEnvironment;
import org.robolectric.Shadows;
import org.robolectric.annotation.Config;
import java.time.Duration;
import static org.junit.Assert.*;
@RunWith(RobolectricTestRunner.class) @Config(sdk=34)
public class AreaCaptionLayerTest {
 @Test public void ConsecutiveRepliesKeepBothSpeakerColours()throws Exception{
  String text="جيسون: افتح الباب.\nلوسيا: أنا قادمة.";
  android.text.Spanned styled=(android.text.Spanned)CaptionVisual.speaker(text,new JSONObject());
  android.text.style.ForegroundColorSpan[] spans=styled.getSpans(0,styled.length(),android.text.style.ForegroundColorSpan.class);
  assertEquals(2,spans.length);assertNotEquals(spans[0].getForegroundColor(),spans[1].getForegroundColor());assertTrue(styled.getSpanStart(spans[1])>text.indexOf('\n'));
 }
 private JSONObject caption(String id,int revision,String text)throws Exception{return new JSONObject("{\"id\":\""+id+"\",\"revision\":"+revision+",\"text\":\""+text+"\",\"duration\":2000,\"output\":{\"left\":0.1,\"top\":0.2,\"right\":0.5,\"bottom\":0.6}}");}
 @Test public void IndependentAreasRejectLateRevisionAndExpireSeparately()throws Exception{
  AreaCaptionLayer layer=new AreaCaptionLayer(RuntimeEnvironment.getApplication());layer.layout(0,0,1920,1080);
  layer.apply(caption("phone",2,"رسالة جديدة"));layer.apply(caption("menu",1,"إعدادات"));layer.apply(caption("phone",1,"رسالة قديمة"));
  assertEquals(2,layer.getChildCount());assertEquals("رسالة جديدة",((TextView)layer.getChildAt(0)).getText().toString());
  Shadows.shadowOf(Looper.getMainLooper()).idleFor(Duration.ofMillis(1200));layer.apply(caption("menu",2,"قائمة جديدة"));
  Shadows.shadowOf(Looper.getMainLooper()).idleFor(Duration.ofMillis(900));assertEquals(View.INVISIBLE,layer.getChildAt(0).getVisibility());assertEquals(View.VISIBLE,layer.getChildAt(1).getVisibility());
 }
 @Test public void ReconnectionClearsOldRevisionHistory()throws Exception{
  AreaCaptionLayer layer=new AreaCaptionLayer(RuntimeEnvironment.getApplication());layer.layout(0,0,1280,720);layer.apply(caption("phone",20,"سابق"));layer.clearAll();layer.apply(caption("phone",1,"جلسة جديدة"));
  assertEquals(1,layer.getChildCount());assertEquals("جلسة جديدة",((TextView)layer.getChildAt(0)).getText().toString());
 }

 @Test public void PhoneCellsKeepTheirOwnPositionsAndDoNotReplaceDialogueChannel()throws Exception{
  AreaCaptionLayer layer=new AreaCaptionLayer(RuntimeEnvironment.getApplication());layer.layout(0,0,1920,1080);
  JSONObject data=caption("phone",1,"رسالتان");
  data.put("panel",new JSONObject("{\"kind\":\"phone\",\"width\":300,\"height\":600,\"cells\":[{\"text\":\"رسالة واردة\",\"foreground\":\"#20232A\",\"background\":\"#FFFFFF\",\"box\":{\"left\":0.1,\"top\":0.2,\"right\":0.9,\"bottom\":0.4}},{\"text\":\"رد صادر\",\"foreground\":\"#FFFFFF\",\"background\":\"#2858E8\",\"box\":{\"left\":0.3,\"top\":0.6,\"right\":0.9,\"bottom\":0.8}}]}"));
  layer.apply(data);android.widget.FrameLayout outer=(android.widget.FrameLayout)layer.getChildAt(0);android.widget.FrameLayout canvas=(android.widget.FrameLayout)outer.getChildAt(0);
  assertEquals(2,canvas.getChildCount());TextView incoming=(TextView)canvas.getChildAt(0),outgoing=(TextView)canvas.getChildAt(1);assertEquals("رسالة واردة",incoming.getText().toString());assertNotEquals(incoming.getCurrentTextColor(),outgoing.getCurrentTextColor());
  assertTrue(((android.widget.FrameLayout.LayoutParams)incoming.getLayoutParams()).topMargin<((android.widget.FrameLayout.LayoutParams)outgoing.getLayoutParams()).topMargin);
 }

 @Test public void CaptionBeforeLayoutIsReplayedAtRealSize()throws Exception{AreaCaptionLayer layer=new AreaCaptionLayer(RuntimeEnvironment.getApplication());layer.apply(caption("notice",1,"نص عربي واضح"));assertEquals(0,layer.getChildCount());layer.layout(0,0,1920,1080);assertEquals(1,layer.getChildCount());assertTrue(layer.getChildAt(0).getLayoutParams().width>100);}
 @Test public void EmptyRichPanelUsesItsArabicTextInsteadOfEmptyBox()throws Exception{AreaCaptionLayer layer=new AreaCaptionLayer(RuntimeEnvironment.getApplication());layer.layout(0,0,1920,1080);JSONObject data=caption("notice",1,"تنبيه مقروء");data.put("panel",new JSONObject("{\"kind\":\"notification\",\"cells\":[]}"));layer.apply(data);assertTrue(layer.getChildAt(0) instanceof TextView);assertEquals("تنبيه مقروء",((TextView)layer.getChildAt(0)).getText().toString());}
}
