// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames;
import java.util.*;
/** Exact normalized matches only; never silently load a Turkish translation pack. */
public final class ArabicPack {
 public final String game;
 private final Map<String,String> lines;
 public ArabicPack(String game,String source,String target,Map<String,String> entries){
  if(!"en".equals(source)||!"ar".equals(target))throw new IllegalArgumentException("Pack must be en -> ar");
  if(entries.size()>20000)throw new IllegalArgumentException("Too many lines");
  Map<String,String> copy=new HashMap<>();
  for(Map.Entry<String,String> e:entries.entrySet()){
   if(e.getKey()==null||e.getValue()==null||e.getKey().length()>2000||e.getValue().length()>3000||e.getKey().trim().isEmpty()||e.getValue().trim().isEmpty())throw new IllegalArgumentException("Invalid line");
   String k=key(e.getKey()),v=e.getValue().trim();
   if(copy.containsKey(k)&&!copy.get(k).equals(v))throw new IllegalArgumentException("Conflicting normalized lines");
   copy.put(k,v);
  }
  this.game=game;this.lines=Collections.unmodifiableMap(copy);
 }
 public static ArabicPack empty(){return new ArabicPack("","en","ar",Collections.emptyMap());}
 private static String key(String s){return s.replace('\u2019','\'').replace('\u2018','\'').replaceAll("\\s+"," ").trim().toLowerCase(Locale.ROOT);}
 public String find(String english){return lines.get(key(english));}
 public int size(){return lines.size();}
}
