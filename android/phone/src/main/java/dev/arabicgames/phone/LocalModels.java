// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames.phone;
import android.content.Context;
import org.json.*;
import java.io.*;
import java.net.*;
import java.nio.charset.StandardCharsets;
import java.nio.file.*;
import java.security.MessageDigest;
import java.util.*;
import java.util.concurrent.atomic.AtomicLong;
import java.util.zip.*;
import com.sal7one.common_jni.translation.Ct2Native;

/** Pinned model data only. No downloaded executable code or arbitrary model paths. */
final class LocalModels {
 interface Progress {void update(String message);}
 static final class Spec {
  final JSONObject json;final String id;final int mode;final JSONArray files;
  Spec(JSONObject value)throws JSONException {json=value;id=value.getString("id");mode=value.getInt("mode");files=value.getJSONArray("files");}
  long size()throws JSONException {long total=0;for(int i=0;i<files.length();i++)total+=files.getJSONObject(i).getLong("bytes");return total;}
 }
 static String packageId(String model){return model.equals("jisr-msa")||model.equals("jisr-saudi")?"jisr-int8":model;}
 static Spec spec(Context c,String model)throws Exception {
  try(InputStream in=c.getAssets().open("native-local-models.json")){
   ByteArrayOutputStream out=new ByteArrayOutputStream();byte[] b=new byte[4096];int n;while((n=in.read(b))!=-1)out.write(b,0,n);
   JSONArray all=new JSONObject(out.toString("UTF-8")).getJSONArray("models");String id=packageId(model);
   for(int i=0;i<all.length();i++){JSONObject value=all.getJSONObject(i);if(value.getString("id").equals(id))return new Spec(value);}
  }throw new IOException("نموذج محلي غير معروف.");
 }
 static File directory(Context c,Spec spec){return new File(c.getFilesDir(),"local-models/"+spec.id);}
 static boolean installed(Context c,String model){try{Spec s=spec(c,model);File d=directory(c,s);if(!new File(d,"verified-v1").isFile())return false;for(int i=0;i<s.files.length();i++){JSONObject f=s.files.getJSONObject(i);File file=new File(d,f.getString("name"));if(!file.isFile()||file.length()!=f.getLong("bytes"))return false;}return true;}catch(Exception e){return false;}}
 static boolean runtime(){try{return android.os.Build.VERSION.SDK_INT>=28&&Ct2Native.INSTANCE.protocolVersion()==2;}catch(LinkageError|RuntimeException e){return false;}}
 static String selected(Context c){return AiPreferences.prefs(c).getString(AiPreferences.NATIVE+".model","quickmt-en-ar");}
 static String hex(byte[] b){StringBuilder s=new StringBuilder();for(byte v:b)s.append(String.format(java.util.Locale.ROOT,"%02x",v&255));return s.toString();}
 static void verify(File file,JSONObject expected)throws Exception {
  if(file.length()!=expected.getLong("bytes"))throw new IOException("حجم ملف النموذج غير مطابق.");
  MessageDigest hash=MessageDigest.getInstance("SHA-256");try(InputStream in=new FileInputStream(file)){byte[] b=new byte[65536];int n;while((n=in.read(b))!=-1)hash.update(b,0,n);}
  if(!hex(hash.digest()).equals(expected.getString("sha256")))throw new IOException("فشل التحقق من سلامة النموذج؛ أعد التنزيل.");
 }
 private static boolean allowed(URL url){String h=url.getHost();return url.getProtocol().equals("https")&&(url.getPort()==-1||url.getPort()==443)&&url.getUserInfo()==null&&(h.equals("huggingface.co")||h.endsWith(".huggingface.co")||h.endsWith(".hf.co")||h.equals("github.com")||h.equals("release-assets.githubusercontent.com")||h.equals("objects.githubusercontent.com"));}
 static void download(JSONObject file,File target,Progress progress)throws Exception {
  if(target.isFile()){try{verify(target,file);return;}catch(Exception bad){if(!target.delete())throw bad;}}
  File part=new File(target.getPath()+".part");long expected=file.getLong("bytes"),offset=part.isFile()?part.length():0;
  if(offset>expected){Files.delete(part.toPath());offset=0;}
  if(offset==expected){try{verify(part,file);Files.move(part.toPath(),target.toPath(),StandardCopyOption.REPLACE_EXISTING);return;}catch(Exception bad){Files.delete(part.toPath());offset=0;}}
  URL url=new URL(file.getString("url"));HttpURLConnection connection=null;
  for(int redirect=0;redirect<6;redirect++){
   if(!allowed(url))throw new IOException("وجهة تنزيل غير مسموحة.");
   connection=(HttpURLConnection)url.openConnection();connection.setInstanceFollowRedirects(false);connection.setConnectTimeout(20000);connection.setReadTimeout(30000);connection.setRequestProperty("Accept-Encoding","identity");connection.setRequestProperty("User-Agent","ArabicGames-Models/1");if(offset>0)connection.setRequestProperty("Range","bytes="+offset+"-");
   int code=connection.getResponseCode();if(code>=300&&code<400){String next=connection.getHeaderField("Location");connection.disconnect();connection=null;if(next==null)throw new IOException("إعادة توجيه غير صالحة.");url=new URL(url,next);continue;}break;
  }
  if(connection==null)throw new IOException("تجاوز حد إعادة التوجيه.");
  try{int code=connection.getResponseCode();boolean append=offset>0&&code==206;
   if(code!=200&&!append)throw new IOException("تعذر التنزيل: HTTP "+code);
   if(append&&!Objects.equals(connection.getHeaderField("Content-Range"),"bytes "+offset+"-"+(expected-1)+"/"+expected))throw new IOException("استئناف غير مطابق.");
   if(!append)offset=0;long count=offset,last=0;
   try(InputStream in=connection.getInputStream();OutputStream out=new FileOutputStream(part,append)){byte[] b=new byte[65536];int n;while((n=in.read(b))!=-1){if(Thread.currentThread().isInterrupted())throw new InterruptedIOException();count+=n;if(count>expected)throw new IOException("تجاوز حجم النموذج.");out.write(b,0,n);long now=android.os.SystemClock.elapsedRealtime();if(now-last>500){progress.update(target.getName()+" · "+(count*100/expected)+"٪");last=now;}}}
   try{verify(part,file);}catch(Exception e){Files.deleteIfExists(part.toPath());throw e;}Files.move(part.toPath(),target.toPath(),StandardCopyOption.REPLACE_EXISTING);
  }finally{connection.disconnect();}
 }
 static void unpack(File archive,File folder,Spec spec)throws Exception {
  Set<String> seen=new HashSet<>();int entries=0;long total=0;
  try(ZipInputStream zip=new ZipInputStream(new FileInputStream(archive))){ZipEntry e;byte[] b=new byte[65536];while((e=zip.getNextEntry())!=null){if(++entries>32)throw new IOException("حزمة تحتوي ملفات كثيرة.");String path=e.getName().replace('\\','/');if(path.startsWith("/")||path.contains(":")||Arrays.asList(path.split("/")).contains(".."))throw new IOException("مسار غير آمن في الحزمة.");if(e.isDirectory())continue;String name=path.substring(path.lastIndexOf('/')+1);JSONObject expected=null;for(int i=0;i<spec.files.length();i++)if(spec.files.getJSONObject(i).getString("name").equals(name))expected=spec.files.getJSONObject(i);
   if(expected==null||!seen.add(name))throw new IOException("ملف زائد أو مكرر في الحزمة.");
   File target=new File(folder,name+".unpack");long count=0;try(OutputStream out=new FileOutputStream(target)){int n;while((n=zip.read(b))!=-1){count+=n;total+=n;if(count>expected.getLong("bytes")||total>spec.size())throw new IOException("حجم حزمة غير مسموح.");out.write(b,0,n);}}
   verify(target,expected);Files.move(target.toPath(),new File(folder,name).toPath(),StandardCopyOption.REPLACE_EXISTING);
  }}if(seen.size()!=spec.files.length())throw new IOException("حزمة النموذج ناقصة.");
 }
 static void install(Context c,String model,android.net.Uri imported,Progress progress)throws Exception {
  Spec s=spec(c,model);File d=directory(c,s);if(!d.isDirectory()&&!d.mkdirs())throw new IOException("تعذر إنشاء مجلد النموذج.");
  Files.deleteIfExists(new File(d,"verified-v1").toPath());long required=s.size()+20_000_000+(imported!=null?s.size()+20_000_000:s.json.has("archive")?s.json.getJSONObject("archive").getLong("bytes"):0);if(d.getUsableSpace()<required)throw new IOException("المساحة غير كافية؛ وفر مساحة للنموذج والتنزيل.");
  if(imported!=null){File archive=new File(d,"import.zip");try(InputStream in=c.getContentResolver().openInputStream(imported);OutputStream out=new FileOutputStream(archive)){if(in==null)throw new IOException("تعذر فتح الملف.");byte[] b=new byte[65536];long total=0;int n;while((n=in.read(b))!=-1){total+=n;if(total>s.size()+20_000_000)throw new IOException("الحزمة أكبر من النموذج المختار.");out.write(b,0,n);}}try{unpack(archive,d,s);}finally{Files.deleteIfExists(archive.toPath());}}
  else if(s.json.has("archive")){File archive=new File(d,"download.zip");download(s.json.getJSONObject("archive"),archive,progress);try{unpack(archive,d,s);}finally{Files.deleteIfExists(archive.toPath());}}
  else for(int i=0;i<s.files.length();i++){JSONObject f=s.files.getJSONObject(i);download(f,new File(d,f.getString("name")),progress);}
  for(int i=0;i<s.files.length();i++){JSONObject f=s.files.getJSONObject(i);verify(new File(d,f.getString("name")),f);}
  Files.write(new File(d,"verified-v1").toPath(),s.id.getBytes(StandardCharsets.UTF_8));
 }
 static final class Session implements AutoCloseable {
  private final AtomicLong handle;final String model;
  Session(Context c,String model)throws Exception {
   if(!runtime())throw new IOException("محرك التشغيل المحلي غير متاح؛ يحتاج نسخة arm64 وأندرويد 9 أو أحدث.");if(!installed(c,model))throw new IOException("نزّل حزمة النموذج المحلي أولًا.");Spec s=spec(c,model);File d=directory(c,s);
   for(int i=0;i<s.files.length();i++){JSONObject f=s.files.getJSONObject(i);verify(new File(d,f.getString("name")),f);}
   this.model=model;long h=Ct2Native.INSTANCE.create(d.getAbsolutePath().getBytes(StandardCharsets.UTF_8),s.mode);if(h==0)throw new IOException("تعذر تحميل النموذج المحلي.");handle=new AtomicLong(h);
  }
  String translate(String text)throws IOException {long h=handle.get();if(h==0)throw new IOException("المحرك متوقف.");byte[] result=Ct2Native.INSTANCE.translate(h,text.getBytes(StandardCharsets.UTF_8),(model.equals("jisr-saudi")?"ars":"ar").getBytes(StandardCharsets.UTF_8));if(result==null||result.length==0)throw new IOException("لم ينتج النموذج ترجمة.");String value=new String(result,StandardCharsets.UTF_8).trim();if(value.isEmpty())throw new IOException("ترجمة فارغة.");return value;}
  public void close(){long h=handle.getAndSet(0);if(h!=0){Ct2Native.INSTANCE.cancel(h);Thread cleanup=new Thread(()->Ct2Native.INSTANCE.destroy(h),"local-model-close");cleanup.start();}}
 }
}
