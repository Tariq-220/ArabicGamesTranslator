// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames;
/** User intent survives a temporary background transition; actual reads never do. */
public final class ReadingSession {
 private boolean requested,visible;private int generation;
 public void start(){requested=true;}
 public void stop(){requested=false;generation++;}
 public void resume(){visible=true;}
 public void pause(){visible=false;generation++;}
 public boolean requested(){return requested;}
 public boolean active(){return requested&&visible;}
 public int generation(){return generation;}
 public boolean accepts(int capturedGeneration){return active()&&capturedGeneration==generation;}
}
