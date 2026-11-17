// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames;
/** Refreshes a still-visible source without translating it twice or replaying another line. */
public final class SubtitleLease {
 private String source="",arabic="";private long sentAt;
 public void store(String source,String arabic,long now){this.source=SubtitleGate.normalize(source);this.arabic=arabic;sentAt=now;}
 public boolean renew(String visibleSource,long now){if(arabic.isEmpty()||!source.equals(SubtitleGate.normalize(visibleSource))||now-sentAt<2000)return false;sentAt=now;return true;}
 public String text(){return arabic;}
 public void clear(){source="";arabic="";}
}
