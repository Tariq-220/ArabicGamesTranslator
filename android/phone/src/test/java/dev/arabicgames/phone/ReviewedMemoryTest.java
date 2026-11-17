package dev.arabicgames.phone;
import org.junit.Test;
import static org.junit.Assert.*;
public class ReviewedMemoryTest {
 @Test public void onlyHumanReviewedExactGameAndTextAreReused()throws Exception{
  DialogueStore store=new DialogueStore(java.io.File.createTempFile("reviewed-memory-",".json"));
  store.add("Red Dead Redemption 2","subtitles","Keep going.","استمر في الذهاب","test");
  assertNull(store.reviewed("Red Dead Redemption 2","Keep going."));
  store.revise(store.snapshot().getJSONObject(0).getLong("id"),"تابع التقدم.");
  assertEquals("تابع التقدم.",store.reviewed("Red Dead Redemption 2","Keep   going."));
  assertNull(store.reviewed("GTA VI","Keep going."));assertNull(store.reviewed("Red Dead Redemption 2","Keep going?"));assertNull(store.reviewed("","Keep going."));
  store.clear();assertNull(store.reviewed("Red Dead Redemption 2","Keep going."));
 }
}
