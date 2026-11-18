package dev.arabicgames.phone;
import org.junit.Test;
import static org.junit.Assert.*;
public class TranslationQualityTest {
 @Test public void flagsObservedLiteralIdiomsWithoutApprovingASubstitute(){
  assertFalse(TranslationQuality.warning("Now kill the engine, get down.","الآن اقتل المحرك، انزل").isEmpty());assertFalse(TranslationQuality.warning("I'm gonna get outta your hair so you can..","سأخرج من شعرك حتى تستطيع").isEmpty());assertFalse(TranslationQuality.warning("You come back with a roll of bills.","تعود بملفات من الفواتير").isEmpty());assertEquals("",TranslationQuality.warning("Now kill the engine, get down.","الآن أطفئ المحرك وانخفض."));
 }
 @Test public void flagsUntranslatedOutputWithoutFlaggingOneProperName(){
  assertFalse(TranslationQuality.warning("Keep going","Keep going").isEmpty());
  assertFalse(TranslationQuality.warning("Keep going","تابع Keep going through palms").isEmpty());
  assertEquals("",TranslationQuality.warning("Arthur","اتبع Arthur الآن"));
 }
}
