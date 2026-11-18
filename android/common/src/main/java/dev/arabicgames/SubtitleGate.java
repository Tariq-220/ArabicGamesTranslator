// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames;
import java.util.LinkedHashMap;
import java.util.Map;
/** Bounded translation memory and repeated-frame suppression. No growing frame queue. */
public final class SubtitleGate {
 private String last = "";
 private String candidate = "";
 private long candidateAt;
 private final Map<String,String> cache = new LinkedHashMap<String,String>(128, .75f, true) {
  protected boolean removeEldestEntry(Map.Entry<String,String> e) { return size() > 256; }
 };
 public static String normalize(String s) { return s.replaceAll("[\\t\\r ]+", " ").replaceAll("\\n+", "\n").trim(); }
 public static String fingerprint(String text){return normalize(text).toLowerCase(java.util.Locale.ROOT).replaceAll("[’'`ʼ]", "").replaceAll("[^a-z0-9\\s]", " ").replaceAll("\\s+", " ").trim();}
 public synchronized boolean accept(String text,long now){return accept(text,now,180);}
 public synchronized boolean accept(String text, long now,long settleMs) {
  text = fingerprint(text);
  if (text.equals("lets") || text.length() < 2 || !text.matches("(?s).*[A-Za-z]{2}.*")) return false;
  if (text.equals(last)) return false;
  if (!text.equals(candidate)) { candidate = text; candidateAt = now; return settleMs==0; }
  if (now - candidateAt < Math.max(0,Math.min(600,settleMs))) return false;
  return true;
 }
 public synchronized boolean alreadyShown(String text) { return last.equals(fingerprint(text)); }
 public synchronized void shown(String text) { last = fingerprint(text); }
 public synchronized void absent() { last = ""; candidate = ""; }
 public synchronized void resetMemory() { absent(); cache.clear(); }
 public synchronized void retry() { candidate = ""; }
 public synchronized String cached(String text) { return cache.get(fingerprint(text)); }
 public synchronized void remember(String text, String arabic) { cache.put(fingerprint(text), arabic); }
 public synchronized int size() { return cache.size(); }
}
