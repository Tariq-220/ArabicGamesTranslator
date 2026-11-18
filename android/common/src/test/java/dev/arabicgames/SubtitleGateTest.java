// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames;
import org.junit.Test;
import static org.junit.Assert.*;
public class SubtitleGateTest {
 @Test public void waitsForStableTextAndSuppressesRepeatedFrames(){SubtitleGate g=new SubtitleGate();assertFalse(g.accept("ARTHUR: We need money.",0));assertFalse(g.accept("ARTHUR: We need money.",100));assertTrue(g.accept("ARTHUR: We need money.",300));g.shown("ARTHUR: We need money.");assertFalse(g.accept("ARTHUR: We need money.",900));}
 @Test public void changedCandidateMustSettleAgain(){SubtitleGate g=new SubtitleGate();g.accept("We need money.",0);assertFalse(g.accept("We need more money.",300));assertTrue(g.accept("We need more money.",600));}
 @Test public void clearAllowsSameLineToAppearAgain(){SubtitleGate g=new SubtitleGate();g.shown("Stay close.");g.absent();assertFalse(g.accept("Stay close.",1000));assertTrue(g.accept("Stay close.",1300));}
 @Test public void memoryIsBoundedAndRetainsArabic(){SubtitleGate g=new SubtitleGate();for(int i=0;i<300;i++)g.remember("line "+i,"السطر "+i);assertEquals(256,g.size());assertNull(g.cached("line 0"));assertEquals("السطر 299",g.cached("line 299"));}
 @Test public void filtersArabicAndTinyNoise(){SubtitleGate g=new SubtitleGate();for(String s:new String[]{"","25","A","هذه ترجمة عربية"}){assertFalse(g.accept(s,0));assertFalse(g.accept(s,300));}}
 @Test public void engineChangeInvalidatesMemoryAndShownLine(){SubtitleGate g=new SubtitleGate();g.remember("Wake up.","ناتج المحرك القديم");g.shown("Wake up.");g.resetMemory();assertNull(g.cached("Wake up."));assertFalse(g.alreadyShown("Wake up."));assertFalse(g.accept("Wake up.",100));assertTrue(g.accept("Wake up.",400));}
 @Test public void normalizesOcrWhitespace(){assertEquals("ARTHUR: Hi\nStay close.",SubtitleGate.normalize(" ARTHUR:  Hi\n\nStay close. "));}
 @Test public void punctuationJitterDoesNotRestartStabilityOrBypassMemory(){SubtitleGate g=new SubtitleGate();assertFalse(g.accept("And why're we doing this?",0,80));assertTrue(g.accept("And whyre we doing this.",100,80));g.remember("And why're we doing this?","لماذا نفعل هذا؟");g.shown("And why're we doing this?");assertTrue(g.alreadyShown("AND WHYRE WE DOING THIS."));assertEquals("لماذا نفعل هذا؟",g.cached("And whyre we doing this"));assertFalse(g.alreadyShown("And whyre we NOT doing this"));}
 @Test public void configurableZeroStabilityAllowsShortDialogueWithoutExtraFrame(){SubtitleGate g=new SubtitleGate();assertFalse(g.accept("Let's",0,0));assertTrue(g.accept("Go!",0,0));assertFalse(g.accept("",100,0));}
}
