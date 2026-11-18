package dev.arabicgames.phone;
import android.content.Context;import org.json.*;import org.junit.*;import org.junit.runner.RunWith;import org.robolectric.*;import org.robolectric.annotation.Config;import static org.junit.Assert.*;import java.io.*;import java.nio.file.*;import java.security.*;import java.util.zip.*;
@RunWith(RobolectricTestRunner.class) @Config(sdk=35)
public class NativeLocalModelsTest {
 private final Context c=RuntimeEnvironment.getApplication();
 @Test public void nativeChoiceNeedsNeitherApiKeyNorCloudConsent()throws Exception {AiPreferences.clear(c);AiPreferences.save(c,AiPreferences.NATIVE,"jisr-msa","Red Dead Redemption 2","",false);AiPreferences.Config config=AiPreferences.read(c);assertEquals("jisr-msa",config.model);assertFalse(config.cloud());assertEquals("",config.key);assertArrayEquals(new String[]{"quickmt-en-ar","jisr-msa","jisr-saudi"},ModelChoices.forProvider(AiPreferences.NATIVE));}
 @Test public void installedMarkerCannotMakeAMissingModelReady()throws Exception {LocalModels.Spec s=LocalModels.spec(c,"jisr-saudi");assertEquals("jisr-int8",s.id);assertEquals(LocalModels.directory(c,s),LocalModels.directory(c,LocalModels.spec(c,"jisr-msa")));File folder=LocalModels.directory(c,s);folder.mkdirs();Files.write(new File(folder,"verified-v1").toPath(),new byte[]{1});assertFalse(LocalModels.installed(c,"jisr-saudi"));}
 @Test public void invalidModelAndChecksumFailClosed()throws Exception {assertThrows(Exception.class,()->LocalModels.spec(c,"../../outside"));File file=File.createTempFile("model", ".bin",c.getCacheDir());Files.write(file.toPath(),new byte[]{1,2,3});JSONObject expected=new JSONObject().put("bytes",3).put("sha256","0".repeat(64));assertThrows(IOException.class,()->LocalModels.verify(file,expected));file.delete();}
 @Test public void importRejectsTraversalAndDuplicateFiles()throws Exception {File archive=File.createTempFile("model", ".zip",c.getCacheDir());LocalModels.Spec spec=LocalModels.spec(c,"jisr-msa");try(ZipOutputStream zip=new ZipOutputStream(new FileOutputStream(archive))){zip.putNextEntry(new ZipEntry("../config.json"));zip.write(new byte[]{1});zip.closeEntry();}assertThrows(IOException.class,()->LocalModels.unpack(archive,c.getCacheDir(),spec));archive.delete();}
 @Test public void existingVerifiedFilesRestoreMarkerWithoutArchiveDownload()throws Exception {
  File folder=Files.createTempDirectory("reuse-model").toFile();byte[] data={1,2,3};
  JSONObject file=new JSONObject().put("name","model.bin").put("bytes",3).put("sha256",LocalModels.hex(MessageDigest.getInstance("SHA-256").digest(data)));
  LocalModels.Spec spec=new LocalModels.Spec(new JSONObject().put("id","test").put("mode",0).put("files",new JSONArray().put(file)));
  Files.write(new File(folder,"model.bin").toPath(),data);
  assertTrue(LocalModels.reuseVerifiedFiles(folder,spec));assertTrue(new File(folder,"verified-v1").isFile());
  Files.write(new File(folder,"model.bin").toPath(),new byte[]{4,5,6});assertFalse(LocalModels.reuseVerifiedFiles(folder,spec));
 }
}
