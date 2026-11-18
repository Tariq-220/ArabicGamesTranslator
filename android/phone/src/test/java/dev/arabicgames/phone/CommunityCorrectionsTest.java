package dev.arabicgames.phone;
import org.junit.*;import static org.junit.Assert.*;import org.json.*;
public class CommunityCorrectionsTest {
 private String pack(boolean reviewed)throws Exception{return new JSONObject().put("format","arabic-games-reviewed-v1").put("source","en").put("target","ar").put("entries",new JSONArray().put(new JSONObject().put("game","RDR2").put("source","Keep going. Through the palms.").put("arabic","تابع عبر النخيل.").put("reviewed",reviewed))).toString();}
 @Test public void unreviewedReportsCannotBecomeAutomaticCorrections()throws Exception {assertThrows(Exception.class,()->CommunityCorrections.parse(pack(false)));var map=CommunityCorrections.parse(pack(true));assertEquals("تابع عبر النخيل.",map.get(CommunityCorrections.key("Red Dead Redemption 2","Keep going. Through the palms.")));assertNull(map.get(CommunityCorrections.key("GTA VI","Keep going. Through the palms.")));}
 @Test public void onlyRightSideInteractionLabelsAreRemoved(){assertTrue(DialogueProfile.interactionPrompt("ROB X",.85f));assertTrue(DialogueProfile.interactionPrompt("Greet Antagonize",.8f));assertFalse(DialogueProfile.interactionPrompt("Greet him at the door.",.9f));assertFalse(DialogueProfile.interactionPrompt("Rob",.5f));}
}
