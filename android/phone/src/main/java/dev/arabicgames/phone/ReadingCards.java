// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames.phone;
import android.content.Context;
import org.json.*;
import java.util.*;
/** Independent normalized source/output boxes. First migration preserves the existing selected reading region. */
final class ReadingCards {
 static final String KEY="cards-v1",BASE="cards-baseline-v1";
 static final int[] COLORS={0xff70dde7,0xfff39aba,0xfff2d87a,0xffa8e6a0,0xffffa27d,0xffc8a2ef,0xff9bd3ff};
 static JSONObject box(double l,double t,double r,double b)throws JSONException{return new JSONObject().put("left",l).put("top",t).put("right",r).put("bottom",b);}
 static JSONObject card(String id,String name,String kind,JSONObject source,JSONObject output,boolean enabled)throws JSONException{return new JSONObject().put("id",id).put("name",name).put("kind",kind).put("source",source).put("output",output).put("enabled",enabled).put("fontSize",28).put("background",kind.equals("phone")?95:72).put("intervalMs",450).put("phoneStyle","panel");}
 static JSONArray defaults(Context c)throws JSONException{
  JSONArray a=new JSONArray();float[] old=ReadingRegion.bounds(c.getSharedPreferences("reading",0).getString("region","subtitles"));String mode=c.getSharedPreferences("reading",0).getString("region","subtitles");
  a.put(card("dialogue","الحوار","dialogue",box(.10,.62,.90,.97),box(.10,.60,.90,.82),true));
  a.put(card("phone","جوال اللعبة","phone",box(.40,.08,.97,.98),box(.08,.12,.42,.94),mode.equals("phoneRight")));
  a.put(card("car-phone","جوال السيارة","phone",box(.03,.10,.60,.98),box(.62,.12,.94,.95),mode.equals("phoneLeft")));
  a.put(card("notice","التنبيهات","notification",box(.68,.54,.99,.99),box(.68,.35,.99,.69),false));
  a.put(card("mission","هدف المهمة","instructions",box(.10,.90,.90,.995),box(.10,.80,.90,.92),false));
  a.put(card("map","وصف المهمة بالخريطة","map",box(.10,.12,.49,.36),box(.10,.37,.49,.64),false));
  a.put(card("menu","القوائم","menu",box(.02,.02,.98,.96),box(.02,.02,.98,.96),mode.equals("all")||mode.equals("left")||mode.equals("right")));
  if(!mode.equals("subtitles")){int index=mode.equals("phoneLeft")?2:mode.equals("phoneRight")?1:6;a.getJSONObject(index).put("source",box(old[0],old[1],old[0]+old[2],old[1]+old[3]));}
  return a;
 }
 static synchronized JSONArray load(Context c){android.content.SharedPreferences p=c.getSharedPreferences("reading",0);try{String raw=p.getString(KEY,null);if(raw!=null)return new JSONArray(raw);JSONArray a=defaults(c);p.edit().putString(KEY,a.toString()).putString(BASE,a.toString()).apply();return a;}catch(JSONException e){try{return defaults(c);}catch(JSONException impossible){return new JSONArray();}}}
 static synchronized void save(Context c,JSONArray cards){c.getSharedPreferences("reading",0).edit().putString(KEY,cards.toString()).apply();CaptureService.readingCardsChanged();}
 static void baseline(Context c,JSONArray cards){android.content.SharedPreferences p=c.getSharedPreferences("reading",0);p.edit().putString("cards-baseline-previous-v1",p.getString(BASE,"")).putString(BASE,cards.toString()).apply();}
 static JSONObject restore(Context c,String id){try{JSONArray a=new JSONArray(c.getSharedPreferences("reading",0).getString(BASE,"[]"));for(int i=0;i<a.length();i++)if(id.equals(a.getJSONObject(i).optString("id")))return a.getJSONObject(i);}catch(JSONException ignored){}return null;}
 static JSONObject dialogue(Context c){JSONArray a=load(c);for(int i=0;i<a.length();i++){JSONObject x=a.optJSONObject(i);if(x!=null&&"dialogue".equals(x.optString("kind")))return x;}return new JSONObject();}
 static float[] bounds(JSONObject card,String key){JSONObject b=card.optJSONObject(key);if(b==null)return new float[]{.1f,.62f,.8f,.35f};float l=clamp(b.optDouble("left",.1)),t=clamp(b.optDouble("top",.62)),r=clamp(b.optDouble("right",.9)),bottom=clamp(b.optDouble("bottom",.97));l=Math.min(l,.97f);t=Math.min(t,.97f);return new float[]{l,t,Math.max(.03f,r-l),Math.max(.03f,bottom-t)};}
 static float clamp(double v){return Double.isFinite(v)?(float)Math.max(0,Math.min(1,v)):0;}
 static float[] primary(Context c){return bounds(dialogue(c),"source");}
 static boolean auxiliaryEnabled(Context c){JSONArray a=load(c);for(int i=0;i<a.length();i++){JSONObject x=a.optJSONObject(i);if(x!=null&&!"dialogue".equals(x.optString("kind"))&&x.optBoolean("enabled"))return true;}return false;}
}
