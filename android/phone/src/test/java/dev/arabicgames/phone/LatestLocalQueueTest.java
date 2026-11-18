package dev.arabicgames.phone;
import org.junit.Test;import static org.junit.Assert.*;
public class LatestLocalQueueTest {
 @Test public void busyInferenceKeepsOnlyNewestSubtitleWithoutBuildingABacklog(){LatestLocalQueue q=new LatestLocalQueue();q.offer("first",1,10,20);var first=q.take();assertNotNull(first);for(int i=0;i<100;i++)q.offer("line "+i,1,30+i,40+i);assertNull(q.take());assertFalse(q.current(first));q.finish(first);var next=q.take();assertEquals("line 99",next.source);assertTrue(q.current(next));q.finish(next);assertNull(q.take());}
 @Test public void repeatedOcrNeverRetranslatesCurrentWork(){LatestLocalQueue q=new LatestLocalQueue();q.offer("same",1,0,1);var job=q.take();for(int i=0;i<50;i++)assertFalse(q.offer("same",1,2,3));assertTrue(q.current(job));q.finish(job);assertNull(q.take());}
 @Test public void resettingDoesNotAllowOverlappingInferenceOrStaleOutput(){LatestLocalQueue q=new LatestLocalQueue();q.offer("old",1,0,1);var old=q.take();q.reset();q.offer("new",2,2,3);assertFalse(q.current(old));assertNull(q.take());q.finish(old);assertEquals("new",q.take().source);}
 @Test public void returningToRunningSubtitleDiscardsTheSupersededWaitingLine(){LatestLocalQueue q=new LatestLocalQueue();q.offer("A",1,0,1);var a=q.take();q.offer("B",1,2,3);q.offer("A",1,4,5);assertTrue(q.current(a));q.finish(a);assertNull(q.take());}
}
