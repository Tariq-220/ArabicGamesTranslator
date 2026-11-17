// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames.phone;
import org.junit.Test;
import static org.junit.Assert.*;
public class PackFilesTest {
 @Test public void parsesArabicPack()throws Exception{assertEquals("مرحبًا",PackFiles.parse("{\"format\":\"arabic-games-pack-v1\",\"source\":\"en\",\"target\":\"ar\",\"lines\":{\"Hello\":\"مرحبًا\"}}").find("Hello"));}
 @Test(expected=IllegalArgumentException.class)public void rejectsWrongLanguage()throws Exception{PackFiles.parse("{\"format\":\"arabic-games-pack-v1\",\"source\":\"en\",\"target\":\"tr\",\"lines\":{\"Hello\":\"Merhaba\"}}");}
}
