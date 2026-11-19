package dev.arabicgames.phone;
import org.junit.Test;import static org.junit.Assert.*;
public class CaptionQualityTest {
 @Test public void hotspotAddressWinsOverCellularAndStationWifi(){assertTrue(Status.networkRank("ap0","192.168.88.1")<Status.networkRank("rmnet_data0","10.115.32.94"));assertTrue(Status.networkRank("swlan0","192.168.43.1")<Status.networkRank("wlan0","192.168.0.118"));assertTrue(Status.networkRank("p2p-wlan0-0","192.168.49.1")<Status.networkRank("rmnet0","10.20.30.4"));}
 @Test public void englishGarbageIsRejectedEvenIfItContainsArabic(){assertTrue(CaptionQuality.reject("Hmm","HTML"));assertTrue(CaptionQuality.reject("[laughs] Ouch.","[يضحك] Ouch."));assertTrue(CaptionQuality.reject("No shit!","No shit!"));assertFalse(CaptionQuality.reject("Jason: Let's go.","Jason: هيا بنا."));assertFalse(CaptionQuality.reject("John Marston","John Marston"));assertFalse(CaptionQuality.reject("GTA VI","GTA VI"));}
 @Test public void wrappedSentenceRemainsOneInference(){assertEquals(1,CaptionQuality.sentences("My phone probably heard me tell Andy\nhow ugly yours was.").size());}
 @Test public void longMultiSentenceCaptionDoesNotLoseItsTail(){var p=CaptionQuality.sentences("It doesn't take seven of you to carry a bag. Bring it out. We need it outside right now.");assertTrue(p.size()>=2);assertEquals("It doesn't take seven of you to carry a bag. Bring it out. We need it outside right now.",String.join(" ",p));assertTrue(p.size()<=4);}
 @Test public void shortCompleteCaptionHasNoExtraModelCall(){assertEquals(1,CaptionQuality.sentences("Hey! What's your deal?").size());}
}
