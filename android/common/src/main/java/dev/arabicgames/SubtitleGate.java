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
 public synchronized boolean accept(String text, long now) {
  text = normalize(text);
  if (text.length() < 4 || !text.matches("(?s).*[A-Za-z]{2}.*")) return false;
  if (text.equals(last)) return false;
  if (!text.equals(candidate)) { candidate = text; candidateAt = now; return false; }
  if (now - candidateAt < 180) return false;
  return true;
 }
 public synchronized boolean alreadyShown(String text) { return last.equals(normalize(text)); }
 public synchronized void shown(String text) { last = normalize(text); }
 public synchronized void absent() { last = ""; candidate = ""; }
 public synchronized void resetMemory() { absent(); cache.clear(); }
 public synchronized void retry() { candidate = ""; }
 public synchronized String cached(String text) { return cache.get(normalize(text)); }
 public synchronized void remember(String text, String arabic) { cache.put(normalize(text), arabic); }
 public synchronized int size() { return cache.size(); }
}
