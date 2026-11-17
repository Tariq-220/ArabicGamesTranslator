// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames.phone;
import dev.arabicgames.ArabicPack;
import org.json.*;
import java.util.*;
final class PackFiles {
 static ArabicPack parse(String json)throws Exception {
  JSONObject root=new JSONObject(json),lines=root.getJSONObject("lines");
  if(!"arabic-games-pack-v1".equals(root.getString("format")))throw new IllegalArgumentException("Unknown format");
  Map<String,String> entries=new HashMap<>();Iterator<String> keys=lines.keys();
  while(keys.hasNext()){String key=keys.next();Object value=lines.get(key);if(!(value instanceof String))throw new IllegalArgumentException("Line must be text");entries.put(key,(String)value);if(entries.size()>20000)throw new IllegalArgumentException("Too many lines");}
  return new ArabicPack(root.optString("game",""),root.getString("source"),root.getString("target"),entries);
 }
}
