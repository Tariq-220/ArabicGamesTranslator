// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames.phone;
/** One running inference and at most one waiting, latest stable subtitle. */
final class LatestLocalQueue {
 static final class Job {final String source;final int generation;final long captured,readDone,sequence;Job(String s,int g,long c,long r,long n){source=s;generation=g;captured=c;readDone=r;sequence=n;}}
 private Job running,pending;private long sequence,latest;
 synchronized boolean offer(String source,int generation,long captured,long readDone){
  if(pending!=null&&pending.generation==generation&&pending.source.equals(source))return false;
  if(running!=null&&running.generation==generation&&running.source.equals(source)){pending=null;latest=running.sequence;return false;}
  pending=new Job(source,generation,captured,readDone,++sequence);latest=pending.sequence;return true;
 }
 synchronized Job take(){if(running!=null||pending==null)return null;running=pending;pending=null;return running;}
 synchronized boolean working(){return running!=null;}
 synchronized boolean current(Job job){return running==job&&job.sequence==latest;}
 synchronized void finish(Job job){if(running==job)running=null;}
 synchronized void reset(){pending=null;latest=++sequence;}
}
