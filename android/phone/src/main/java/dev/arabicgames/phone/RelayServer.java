// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames.phone;
import fi.iki.elonen.NanoHTTPD;
import org.json.JSONObject;
import java.io.*;
import java.nio.charset.StandardCharsets;
import java.util.*;
import java.util.concurrent.*;

final class RelayServer extends NanoHTTPD {
 final String code;
 static volatile RelayServer active;
 private String pendingTicket,pendingIp;private long pendingUntil;private boolean approved;
 synchronized String pendingChallenge(){return pendingTicket!=null&&System.nanoTime()/1_000_000<pendingUntil&&!approved?pendingTicket.substring(0,6):null;}
 synchronized void approve(String challenge,boolean accept){if(challenge.equals(pendingChallenge())){if(accept)approved=true;else pendingUntil=0;}}
 private Response pairing(IHTTPSession s){
  if(!dev.arabicgames.Pairing.privateIpv4(s.getRemoteIpAddress())||s.getHeaders().containsKey("origin"))return newFixedLengthResponse(Response.Status.FORBIDDEN,"text/plain","Local app only");
  if("/hello".equals(s.getUri()))return newFixedLengthResponse(Response.Status.OK,"application/json","{\"app\":\"arabicgames-phone\",\"pairing\":1}");
  String ticket=s.getParms().get("ticket");if(ticket==null||!ticket.matches("[a-f0-9]{32}"))return newFixedLengthResponse(Response.Status.BAD_REQUEST,"text/plain","Invalid ticket");
  long now=System.nanoTime()/1_000_000;
  if(pendingTicket==null||now>=pendingUntil){pendingTicket=ticket;pendingIp=s.getRemoteIpAddress();pendingUntil=now+60000;approved=false;}
  if(!ticket.equals(pendingTicket)||!s.getRemoteIpAddress().equals(pendingIp))return newFixedLengthResponse(Response.Status.SERVICE_UNAVAILABLE,"text/plain","Pairing busy");
  Response r=newFixedLengthResponse(Response.Status.OK,"application/json",approved?"{\"code\":\""+code+"\"}":"{\"pending\":true}");r.addHeader("Cache-Control","no-store");return r;
 }
 private String lastText;private long lastUntil,revision;
 private final Map<String,Long> pollingSeen=new ConcurrentHashMap<>();
 private final Set<Stream> streams = ConcurrentHashMap.newKeySet();
 private final Map<String,Stream> receivers=new ConcurrentHashMap<>();
 private final long heartbeatMs;
 private final Map<String,long[]> attempts = new LinkedHashMap<>();
 RelayServer(String code) { this(code,8787); }
 RelayServer(String code,int port) { this(code,port,2500); }
 RelayServer(String code,int port,long heartbeatMs) { super(port); this.code=code; this.heartbeatMs=heartbeatMs; active=this; }
 int clients() { long now=System.nanoTime()/1_000_000;pollingSeen.entrySet().removeIf(e->now-e.getValue()>5000);Set<String> active=new HashSet<>(receivers.keySet());active.addAll(pollingSeen.keySet());return active.size(); }
 private synchronized boolean allowed(String ip, String supplied) {
  long now=System.nanoTime()/1_000_000;
  long[] a=attempts.get(ip);
  if(a==null || now-a[0]>60000) { if(attempts.size()>256)attempts.clear(); a=new long[]{now,0};attempts.put(ip,a); }
  if(a[1]>=8)return false;
  if(!code.equals(supplied)){a[1]++;return false;}return true;
 }
 @Override public synchronized Response serve(IHTTPSession session) {
  if(session.getMethod()!=Method.GET)return newFixedLengthResponse(Response.Status.METHOD_NOT_ALLOWED,"text/plain","GET only");
  if("/hello".equals(session.getUri())||"/pair".equals(session.getUri()))return pairing(session);
  if(!allowed(session.getRemoteIpAddress(),session.getParms().get("code")))return newFixedLengthResponse(Response.Status.FORBIDDEN,"text/plain","Pairing code required; retry after 60 seconds");
  if("/screen".equals(session.getUri())){Response page=newFixedLengthResponse(Response.Status.OK,"text/html; charset=utf-8",SecondScreenPage.HTML);page.addHeader("Cache-Control","no-store");page.addHeader("Referrer-Policy","no-referrer");page.addHeader("X-Frame-Options","DENY");return page;}
  String receiver=session.getParms().get("receiver");if(receiver==null||!receiver.matches("[A-Za-z0-9-]{8,80}"))receiver=session.getRemoteIpAddress();
  if("/latest".equals(session.getUri())){
   if(pollingSeen.size()<16||pollingSeen.containsKey(receiver))pollingSeen.put(receiver,System.nanoTime()/1_000_000);
   long left=lastUntil-System.nanoTime()/1_000_000;boolean active=lastText!=null&&left>0;
   try{String json=new JSONObject().put("revision",revision).put("active",active).put("text",active?lastText:"").put("duration",active?left:0).toString();Response r=newFixedLengthResponse(Response.Status.OK,"application/json; charset=utf-8",json);r.addHeader("Cache-Control","no-store");return r;}catch(Exception e){return newFixedLengthResponse(Response.Status.INTERNAL_ERROR,"text/plain","Unavailable");}
  }
  if(!"/events".equals(session.getUri()))return newFixedLengthResponse(Response.Status.NOT_FOUND,"text/plain","Not found");
  Stream old=receivers.remove(receiver);if(old!=null)old.close();
  if(streams.size()>=8)return newFixedLengthResponse(Response.Status.SERVICE_UNAVAILABLE,"text/plain","Receiver limit reached");
  Stream stream=new Stream(receiver);streams.add(stream);receivers.put(receiver,stream);
  long remaining=lastUntil-System.nanoTime()/1_000_000;if(lastText!=null&&remaining>0)stream.offer(caption(lastText,(int)remaining,revision));
  Response r=newChunkedResponse(Response.Status.OK,"text/event-stream; charset=utf-8",stream);
  r.addHeader("Cache-Control","no-store");r.addHeader("X-Content-Type-Options","nosniff");return r;
 }
 synchronized void subtitle(String arabic,int ms) { lastText=arabic;lastUntil=System.nanoTime()/1_000_000+ms;revision++;broadcast("subtitle",captionJson(arabic,ms,revision)); }
 private static String captionJson(String text,int ms,long revision){try{return new JSONObject().put("text",text).put("duration",ms).put("revision",revision).toString();}catch(Exception e){return "{}";}}
 private static byte[] caption(String text,int ms,long revision){return ("event: subtitle\ndata: "+captionJson(text,ms,revision)+"\n\n").getBytes(StandardCharsets.UTF_8);}
 synchronized void area(JSONObject value){broadcast("area",value.toString());}
 synchronized void openTvPanel(){broadcast("settings","{}");}
 synchronized void clear() { lastText=null;lastUntil=0;revision++;broadcast("clear","{\"revision\":"+revision+"}"); }
 private void broadcast(String event,String json) { byte[] data=("event: "+event+"\ndata: "+json+"\n\n").getBytes(StandardCharsets.UTF_8);for(Stream s:streams)s.offer(data); }
 @Override public void stop() { if(active==this)active=null; for(Stream s:streams)s.close();pollingSeen.clear();super.stop(); }
 private final class Stream extends InputStream {
  final String receiver;
  Stream(String receiver){this.receiver=receiver;}
  final ArrayBlockingQueue<byte[]> queue=new ArrayBlockingQueue<>(4);
  volatile boolean closed;byte[] current=new byte[0];int pos;
  void offer(byte[] data) { if(!queue.offer(data)){queue.poll();queue.offer(data);} }
  public int read() throws IOException { byte[] b=new byte[1];int n=read(b,0,1);return n<0?-1:b[0]&255; }
  public int read(byte[] b,int off,int len)throws IOException {
   if(len==0)return 0;if(closed)return -1;
   if(pos>=current.length) {
    try { current=queue.poll(heartbeatMs,TimeUnit.MILLISECONDS); }
    catch(InterruptedException e){Thread.currentThread().interrupt();throw new IOException("Stopped",e);}
    if(closed)return -1;
    if(current==null)current=": heartbeat\n\n".getBytes(StandardCharsets.UTF_8);pos=0;
   }
   int n=Math.min(len,current.length-pos);System.arraycopy(current,pos,b,off,n);pos+=n;return n;
  }
  public void close(){closed=true;streams.remove(this);receivers.remove(receiver,this);queue.offer(new byte[0]);}
 }
}
