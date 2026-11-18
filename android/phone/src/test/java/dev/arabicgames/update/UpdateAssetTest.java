package dev.arabicgames.update;
import org.json.*;import org.junit.*;import static org.junit.Assert.*;
public class UpdateAssetTest {
 private JSONObject asset(String name)throws Exception{return new JSONObject().put("name",name).put("digest","sha256:"+"a".repeat(64)).put("size",123).put("browser_download_url","https://github.com/"+UpdateActivity.REPOSITORY+"/releases/download/v0.4.15/"+name);}
 private JSONArray releases(JSONObject...assets)throws Exception{JSONArray a=new JSONArray();for(JSONObject v:assets)a.put(v);return new JSONArray().put(new JSONObject().put("tag_name","v0.4.15").put("assets",a));}
 @Test public void choosesOnlyNewerOwnPlatform()throws Exception{JSONArray all=releases(asset("ArabicGames-Phone-0.4.7-arm64.apk"),asset("ArabicGames-TV-0.3.5.apk"));assertEquals("0.4.7",UpdateActivity.choose(all,"Phone","0.4.6").version);assertEquals("0.3.5",UpdateActivity.choose(all,"TV","0.3.4").version);assertNull(UpdateActivity.choose(all,"Phone","0.4.7"));}
 @Test public void rejectsTamperedOriginsAndMissingDigest()throws Exception{JSONObject a=asset("ArabicGames-Phone-0.4.7-arm64.apk");a.put("browser_download_url","https://evil.example/update.apk");assertThrows(Exception.class,()->UpdateActivity.choose(releases(a),"Phone","0.4.6"));a.put("browser_download_url","https://github.com/"+UpdateActivity.REPOSITORY+"/releases/download/v0.4.15/"+a.getString("name")).remove("digest");assertThrows(Exception.class,()->UpdateActivity.choose(releases(a),"Phone","0.4.6"));}
}
