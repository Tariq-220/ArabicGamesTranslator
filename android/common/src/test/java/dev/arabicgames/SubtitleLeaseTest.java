// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames;
import org.junit.Test;
import static org.junit.Assert.*;
public class SubtitleLeaseTest {
 @Test public void holdsSameLineWithoutRepeatedTranslation(){SubtitleLease l=new SubtitleLease();l.store("Stay close.","ابق قريبًا.",0);assertFalse(l.renew("Stay close.",1000));assertTrue(l.renew("Stay close.",2000));assertEquals("ابق قريبًا.",l.text());assertFalse(l.renew("Another line.",5000));}
 @Test public void pauseOrMissingTextClearsOldTranslation(){SubtitleLease l=new SubtitleLease();l.store("Hi.","مرحبًا",0);l.clear();assertFalse(l.renew("Hi.",3000));assertEquals("",l.text());}
}
