// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames.phone;
import android.content.Context;
import org.json.*;
import java.io.*;
import java.nio.charset.StandardCharsets;
import java.util.*;
import java.util.concurrent.*;

/** Bounded private history; no frame, account data or API credentials. */
final class DialogueStore {
 private static DialogueStore instance;
 static synchronized DialogueStore get(Context c){if(instance==null)instance=new DialogueStore(new File(c.getFilesDir(),"dialogue-history.json"));return instance;}
 private final File file;private final ArrayList<JSONObject> lines=new ArrayList<>();
 private final ExecutorService writer=Executors.newSingleThreadExecutor(r->{Thread t=new Thread(r,"dialogue-storage");t.setDaemon(true);return t;});
 private long revision,nextId;private boolean writing;
 DialogueStore(File file){this.file=file;try{if(file.exists()&&file.length()<4_000_000){JSONArray stored=new JSONArray(new String(java.nio.file.Files.readAllBytes(file.toPath()),StandardCharsets.UTF_8));for(int i=Math.max(0,stored.length()-200);i<stored.length();i++){JSONObject entry=stored.getJSONObject(i);lines.add(entry);nextId=Math.max(nextId,entry.optLong("id"));}}}catch(Exception ignored){lines.clear();}}
 synchronized void add(String game,String mode,String source,String arabic,String engine){
  if(source.length()>2000||arabic.length()>3000)return;
  game=game.substring(0,Math.min(200,game.length()));engine=engine.substring(0,Math.min(200,engine.length()));
  if(!lines.isEmpty()){JSONObject previous=lines.get(lines.size()-1);if(source.equals(previous.optString("source"))&&arabic.equals(previous.optString("arabic"))&&game.equals(previous.optString("game"))&&mode.equals(previous.optString("mode")))return;}
  try{lines.add(new JSONObject().put("id",++nextId).put("time",System.currentTimeMillis()).put("game",game).put("mode",mode).put("source",source).put("arabic",arabic).put("engine",engine).put("reviewed",false));while(lines.size()>200)lines.remove(0);changed();}catch(JSONException ignored){}
 }
 synchronized JSONArray snapshot(){try{return new JSONArray(new JSONArray(lines).toString());}catch(JSONException e){return new JSONArray();}}
 synchronized void revise(long id,String arabic){if(arabic.trim().isEmpty()||arabic.length()>3000)throw new IllegalArgumentException();for(JSONObject entry:lines)if(entry.optLong("id")==id){try{entry.put("arabic",arabic.trim()).put("reviewed",true);changed();}catch(JSONException ignored){}return;}}
 synchronized String reviewed(String game,String source){
  String key=source.trim().replaceAll("\\s+"," ");if(game==null||game.isBlank()||key.isEmpty())return null;
  for(int i=lines.size()-1;i>=0;i--){JSONObject e=lines.get(i);if(e.optBoolean("reviewed")&&game.equals(e.optString("game"))&&key.equals(e.optString("source").trim().replaceAll("\\s+"," ")))return e.optString("arabic");}return null;
 }
 synchronized void clear(){lines.clear();changed();}
 synchronized String pack(String game,boolean reviewedOnly)throws Exception{
  JSONObject pairs=new JSONObject();Set<String> conflicts=new HashSet<>();
  for(JSONObject e:lines)if(game.equals(e.optString("game"))&&(!reviewedOnly||e.optBoolean("reviewed"))){String source=e.getString("source"),arabic=e.getString("arabic");if(pairs.has(source)&&!pairs.getString(source).equals(arabic))conflicts.add(source);else pairs.put(source,arabic);}
  for(String source:conflicts)pairs.remove(source);
  if(pairs.length()==0)throw new IllegalArgumentException("No lines");
  return new JSONObject().put("format","arabic-games-pack-v1").put("game",game).put("source","en").put("target","ar").put("reviewed_only",reviewedOnly).put("notice",reviewedOnly?"User reviewed":"Machine translations: review before reuse").put("lines",pairs).toString(2);
 }
 private void changed(){revision++;if(writing)return;writing=true;writer.execute(()->{
  while(true){long saved;String json;synchronized(this){saved=revision;json=snapshot().toString();}
   try{File temp=new File(file.getPath()+".tmp");java.nio.file.Files.write(temp.toPath(),json.getBytes(StandardCharsets.UTF_8));java.nio.file.Files.move(temp.toPath(),file.toPath(),java.nio.file.StandardCopyOption.REPLACE_EXISTING);}catch(IOException ignored){}
   synchronized(this){if(saved==revision){writing=false;return;}}
  }
 });}
}
