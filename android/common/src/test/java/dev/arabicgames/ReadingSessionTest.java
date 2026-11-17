// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames;
import org.junit.Test;
import static org.junit.Assert.*;
public class ReadingSessionTest {
 @Test public void repeatedStartNeverTogglesReadingOff(){ReadingSession s=new ReadingSession();s.resume();s.start();s.start();assertTrue(s.active());s.stop();assertFalse(s.active());}
 @Test public void temporaryPausePreservesIntentButRejectsOldFrame(){ReadingSession s=new ReadingSession();s.resume();s.start();int before=s.generation();s.pause();assertFalse(s.active());assertTrue(s.requested());s.resume();assertTrue(s.active());assertFalse(s.accepts(before));assertTrue(s.accepts(s.generation()));}
 @Test public void explicitStopIsNotUndoneByResume(){ReadingSession s=new ReadingSession();s.resume();s.start();s.stop();s.pause();s.resume();assertFalse(s.active());}
}
