package dev.arabicgames.phone;
import org.junit.Test;import static org.junit.Assert.*;
public class DialogueWorkQueueTest {
 @Test public void RepliesAndReadyTranslationsRemainInOrder(){DialogueWorkQueue q=new DialogueWorkQueue();assertTrue(q.offer("Jason: Wait here.",1,0,10));DialogueWorkQueue.Job first=q.take();assertTrue(q.offer("Lucia: No!",1,20,30));assertTrue(q.offer("Jason: Fine.",1,40,50,"حسنًا.","حزمة"));assertTrue(q.current(first));assertNull(q.take());q.finish(first);DialogueWorkQueue.Job second=q.take();assertEquals("Lucia: No!",second.source);q.finish(second);DialogueWorkQueue.Job third=q.take();assertEquals("Jason: Fine.",third.source);assertEquals("حسنًا.",third.ready);}
 @Test public void DuplicateReadsDoNotConsumeReplySlotsAndResetCancelsOldGeneration(){DialogueWorkQueue q=new DialogueWorkQueue();q.offer("Wait here.",1,0,10);DialogueWorkQueue.Job first=q.take();assertFalse(q.offer("Wait here.",1,20,30));assertTrue(q.offer("No!",1,40,50));q.reset();assertFalse(q.current(first));q.finish(first);assertNull(q.take());assertTrue(q.offer("A new reply.",2,60,70));assertEquals(2,q.take().generation);}
}
