package dev.arabicgames.phone;
import static org.junit.Assert.*;
import android.content.Context;import android.graphics.RectF;import android.view.View;
import org.json.*;import org.junit.Test;import org.junit.runner.RunWith;import org.robolectric.*;
import java.lang.reflect.*;import java.util.*;import java.util.concurrent.*;
@RunWith(RobolectricTestRunner.class)
public class PhoneUpdate061Test {
 @Test public void MigrationKeepsTheCurrentCarPhoneRegionAndIndependentOutput()throws Exception {
  Context c=RuntimeEnvironment.getApplication();c.getSharedPreferences("reading",0).edit().clear().putString("region","phoneLeft").commit();
  JSONArray cards=ReadingCards.load(c);JSONObject phone=cards.getJSONObject(2);assertTrue(phone.getBoolean("enabled"));
  assertArrayEquals(ReadingRegion.bounds("phoneLeft"),ReadingCards.bounds(phone,"source"),.0001f);
  assertNotEquals(phone.getJSONObject("source").toString(),phone.getJSONObject("output").toString());
  JSONObject original=ReadingCards.restore(c,"car-phone");phone.put("fontSize",40);ReadingCards.save(c,cards);assertEquals(28,original.getInt("fontSize"));assertEquals(28,ReadingCards.restore(c,"car-phone").getInt("fontSize"));
  ReadingCards.baseline(c,cards);assertEquals(40,ReadingCards.restore(c,"car-phone").getInt("fontSize"));
 }
 @Test public void UnchangedAreaLeaseRetainsTheSameBubblesRatherThanRebuildingThem()throws Exception {
  AreaCaptionLayer layer=new AreaCaptionLayer(RuntimeEnvironment.getApplication());layer.layout(0,0,1920,1080);
  JSONObject value=new JSONObject().put("id","phone").put("revision",1).put("text","رسالة").put("output",ReadingCards.box(.1,.1,.5,.8)).put("duration",3000);
  layer.apply(value);assertEquals(1,layer.getChildCount());View original=layer.getChildAt(0);
  value.put("revision",2);layer.apply(value);assertSame(original,layer.getChildAt(0));
  value.put("revision",3).put("clear",true);layer.apply(value);assertEquals(0,layer.getChildCount());
 }
 @Test public void MapAndMissionCannotBorrowPhoneMessagesEvenWithDifferentOcrSpellings()throws Exception {
  Context c=RuntimeEnvironment.getApplication();JSONArray cards=new JSONArray();
  cards.put(ReadingCards.card("phone","الجوال","phone",ReadingCards.box(0,0,1,1),ReadingCards.box(.05,.1,.4,.9),true));
  cards.put(ReadingCards.card("map","الخريطة","map",ReadingCards.box(0,0,1,1),ReadingCards.box(.5,.1,.9,.7),true));
  cards.put(ReadingCards.card("mission","المهمة","instructions",ReadingCards.box(0,0,1,1),ReadingCards.box(.1,.8,.9,.9),true));ReadingCards.save(c,cards);
  List<JSONObject> outputs=new ArrayList<>();List<String> requests=new ArrayList<>();Map<String,CompletableFuture<String>> pending=new HashMap<>();
  PhoneAreas areas=new PhoneAreas(c,text->{requests.add(text);CompletableFuture<String> job=new CompletableFuture<>();pending.put(text,job);return job;},outputs::add,null);
  Field field=PhoneAreas.class.getDeclaredField("states");field.setAccessible(true);List<?> states=(List<?>)field.get(areas);
  Method accept=PhoneAreas.class.getDeclaredMethod("accept",states.get(0).getClass(),long.class,List.class);accept.setAccessible(true);
  accept.invoke(areas,states.get(0),0L,new ArrayList<>(Arrays.asList(row("Jason",.02f,.08f,"#EEEEEE"),row("Almost there!",.2f,.3f,"#EEEEEE"),row("I'm on it.",.5f,.6f,"#EEEEEE"))));
  org.robolectric.shadows.ShadowSystemClock.advanceBy(java.time.Duration.ofMillis(200));
  accept.invoke(areas,states.get(0),0L,new ArrayList<>(Arrays.asList(row("Jason",.02f,.08f,"#EEEEEE"),row("Almost there!",.2f,.3f,"#EEEEEE"),row("I'm on it.",.5f,.6f,"#EEEEEE"))));
  for(int i=1;i<3;i++)accept.invoke(areas,states.get(i),0L,new ArrayList<>(Arrays.asList(row("Dropped off scuba at your boat.",.21f,.29f,"#20232A"))));
  org.robolectric.shadows.ShadowSystemClock.advanceBy(java.time.Duration.ofMillis(200));
  for(int i=1;i<3;i++)accept.invoke(areas,states.get(i),0L,new ArrayList<>(Arrays.asList(row("Dropped off scuba at your boat.",.21f,.29f,"#20232A"))));
  assertEquals(java.util.Collections.singletonList("Jason"),requests);assertTrue(outputs.stream().noneMatch(x->"map".equals(x.optString("id"))||"mission".equals(x.optString("id"))));cards.getJSONObject(1).put("fontSize",33);ReadingCards.save(c,cards);areas.reload();pending.get("Jason").complete("جيسون");assertTrue(requests.contains("Almost there!"));pending.get("Almost there!").complete("أوشكت على الوصول!");assertTrue(outputs.stream().anyMatch(x->"phone".equals(x.optString("id"))&&!x.optBoolean("clear")));areas.close();
 }
 @Test public void NavigationInEitherOrderAndRecordedButtonsAreNotAuxiliaryMessages(){assertTrue(PhoneAreas.navigation("PROGRESS\nBRIEF\nROBBERY OPPORTUNITY"));assertTrue(PhoneAreas.navigation("BRIEF\nPROGRESS"));assertTrue(PhoneAreas.control("EXIT O"));assertTrue(PhoneAreas.control("PHOTO STUDIO O SELEC"));assertFalse(PhoneAreas.control("Get in the car."));}
 @Test public void FormalLexiconDoesNotHideSentencesOrChangeUnknownNames(){assertEquals("ابن العاهرة",FormalArabic.normalize("ابن القحبة"));assertEquals("جيسون",FormalArabic.ready("Jason"));assertEquals("السكان: تبًّا لك، أيها الوغد!",FormalArabic.ready("Locals: Suck my fuckin' nuts!"));assertNull(FormalArabic.ready("Jason: He said that and walked away."));}
 @Test public void WrappedLinesMergeWithinOneBubbleButSeparateMessagesRemainSeparate(){List<PhoneAreas.Row> rows=new ArrayList<>(Arrays.asList(row("Dropped off scuba",.20f,.25f,"#EEEEEE"),row("at your boat.",.26f,.31f,"#EEEEEE"),row("I'm on it.",.37f,.43f,"#99BBFF")));List<PhoneAreas.Row> result=PhoneAreas.groupRows(rows);assertEquals(2,result.size());assertEquals("Dropped off scuba at your boat.",result.get(0).source);assertEquals("I'm on it.",result.get(1).source);}
 private static PhoneAreas.Row row(String text,float top,float bottom,String background){return new PhoneAreas.Row(text,new RectF(.1f,top,.9f,bottom),background,"#20232A",false);}
}
