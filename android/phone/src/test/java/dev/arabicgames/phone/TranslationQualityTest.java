package dev.arabicgames.phone;
import org.junit.Test;
import static org.junit.Assert.*;
public class TranslationQualityTest {
 @Test public void flagsUntranslatedOutputWithoutFlaggingOneProperName(){
  assertFalse(TranslationQuality.warning("Keep going","Keep going").isEmpty());
  assertFalse(TranslationQuality.warning("Keep going","تابع Keep going through palms").isEmpty());
  assertEquals("",TranslationQuality.warning("Arthur","اتبع Arthur الآن"));
 }
}
