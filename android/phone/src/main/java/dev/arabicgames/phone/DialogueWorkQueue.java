// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames.phone;
import java.util.ArrayDeque;
/** One inference and two distinct replies in order. A newer reply does not invalidate the running one. */
final class DialogueWorkQueue {
 static final class Job {final String source,ready,origin;final int generation;final long captured,readDone;boolean cancelled;Job(String s,int g,long c,long r,String ready,String origin){source=s;generation=g;captured=c;readDone=r;this.ready=ready;this.origin=origin;}}
 private Job running;private final ArrayDeque<Job> pending=new ArrayDeque<>();
 private static boolean same(String a,String b){return dev.arabicgames.SubtitleGate.fingerprint(a).equals(dev.arabicgames.SubtitleGate.fingerprint(b));}
 synchronized boolean offer(String s,int generation,long captured,long readDone){return offer(s,generation,captured,readDone,null,null);}
 synchronized boolean offer(String s,int generation,long captured,long readDone,String ready,String origin){if(running!=null&&!running.cancelled&&running.generation==generation&&same(running.source,s))return false;for(Job job:pending)if(job.generation==generation&&same(job.source,s))return false;if(pending.size()>=2){pending.removeFirst();PipelineMetrics.superseded.incrementAndGet();}pending.addLast(new Job(s,generation,captured,readDone,ready,origin));return true;}
 synchronized Job take(){if(running!=null||pending.isEmpty())return null;return running=pending.removeFirst();}
 synchronized boolean working(){return running!=null||!pending.isEmpty();}
 synchronized boolean current(Job job){return running==job&&!job.cancelled;}
 synchronized void finish(Job job){if(running==job)running=null;}
 synchronized void reset(){pending.clear();if(running!=null)running.cancelled=true;}
}
