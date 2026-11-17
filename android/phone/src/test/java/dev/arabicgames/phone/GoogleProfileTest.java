package dev.arabicgames.phone;
import org.junit.Test;
import org.junit.runner.RunWith;
import org.robolectric.*;
import org.robolectric.annotation.Config;
import static org.junit.Assert.*;
@RunWith(RobolectricTestRunner.class) @Config(sdk=35)
public class GoogleProfileTest {
 @Test public void googleSavesWithoutKeyButRequiresConsent()throws Exception{android.content.Context c=RuntimeEnvironment.getApplication();AiPreferences.clear(c);try{AiPreferences.save(c,AiPreferences.GOOGLE,"google-gtx","RDR2","",false);fail();}catch(IllegalArgumentException expected){}AiPreferences.save(c,AiPreferences.GOOGLE,"google-gtx","RDR2","",true);AiPreferences.Config config=AiPreferences.read(c);assertTrue(config.cloud());assertEquals("",config.key);assertEquals(AiPreferences.GOOGLE,config.provider);}
 @Test public void parsesBothGoogleResponseShapes()throws Exception{assertEquals("مرحبًا",GoogleGtx.parse("[[[\"مرحبًا\",\"Hello\"]]]"));assertEquals("مرحبًا",GoogleGtx.parse("[\"مرحبًا\"]"));}
 @Test public void correctionsAreScopedAndDoNotMergeDifferentDialogue(){assertEquals("I did.",DialogueProfile.english("1 did.","RDR2"));assertEquals("1 did.",DialogueProfile.english("1 did.","Other"));assertEquals("داتش",DialogueProfile.arabic("Dutch","الهولندية","RDR2"));assertEquals("الهولندية",DialogueProfile.arabic("Dutch","الهولندية","Other"));assertEquals("We lost.",DialogueProfile.english("We lost.","RDR2"));}
}
