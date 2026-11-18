// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames.phone;
import org.json.*;import java.io.*;import java.nio.charset.StandardCharsets;import java.nio.file.*;
/** Durable personal corrections, separate from the rolling 200-line history. */
final class ReviewedPairs {
 private final File file;private JSONObject pairs=new JSONObject();
 ReviewedPairs(File file){this.file=file;try{if(file.isFile()&&file.length()<=8_000_000)pairs=new JSONObject(new String(Files.readAllBytes(file.toPath()),StandardCharsets.UTF_8));}catch(Exception ignored){}}
 private static String key(String game,String source){return CommunityCorrections.key(game,source);}
 synchronized String get(String game,String source){JSONObject e=pairs.optJSONObject(key(game,source));return e==null?null:e.optString("arabic",null);}
 synchronized void put(String game,String source,String arabic){try{JSONObject copy=new JSONObject(pairs.toString());copy.put(key(game,source),new JSONObject().put("game",game).put("source",source).put("arabic",arabic));if(copy.length()>20000)throw new IllegalArgumentException("Too many corrections");persist(copy);pairs=copy;}catch(JSONException e){throw new IllegalStateException(e);}}
 synchronized JSONArray forGame(String game){JSONArray out=new JSONArray();java.util.Iterator<String> keys=pairs.keys();while(keys.hasNext()){JSONObject e=pairs.optJSONObject(keys.next());if(e!=null&&GameNames.same(game,e.optString("game")))out.put(e);}return out;}
 synchronized void clear(){JSONObject empty=new JSONObject();persist(empty);pairs=empty;}
 private void persist(JSONObject value){byte[] bytes=value.toString().getBytes(StandardCharsets.UTF_8);if(bytes.length>8_000_000)throw new IllegalArgumentException("Correction store full");File temp=new File(file.getPath()+".tmp");try{try(FileOutputStream out=new FileOutputStream(temp)){out.write(bytes);out.getFD().sync();}Files.move(temp.toPath(),file.toPath(),StandardCopyOption.REPLACE_EXISTING);}catch(IOException e){throw new IllegalStateException("Cannot save personal correction",e);}}
}
