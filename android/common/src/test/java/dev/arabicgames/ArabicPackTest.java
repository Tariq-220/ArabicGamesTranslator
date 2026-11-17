// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames;
import org.junit.Test;
import static org.junit.Assert.*;
import java.util.*;
public class ArabicPackTest {
 @Test public void matchesNormalizedEnglishAndPreservesArabic(){ArabicPack p=new ArabicPack("RDR2 test","en","ar",Map.of("ARTHUR: We need money.","آرثر: نحتاج إلى المال."));assertEquals("آرثر: نحتاج إلى المال.",p.find(" arthur:   We need money. "));assertNull(p.find("ARTHUR: We need more money."));}
 @Test(expected=IllegalArgumentException.class)public void rejectsTurkishPack(){new ArabicPack("RDR2","en","tr",Map.of("Stay close.","Yakın dur."));}
 @Test(expected=IllegalArgumentException.class)public void rejectsConflictingLines(){new ArabicPack("test","en","ar",Map.of("Hi.","مرحبًا","hi.","أهلًا"));}
}
