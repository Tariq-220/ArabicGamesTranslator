// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames.phone;
import org.json.*;import java.util.*;
/** Compare only the same English dialogue in the same game. Ratings are user supplied. */
final class EngineComparison {
 static String engine(String name){String n=name.toLowerCase(Locale.ROOT);if(n.contains("ذاكرة")||n.contains("تصحيح")||n.contains("حزمة"))return "";return n.contains("quickmt")?"QuickMT":n.contains("deepl")?"DeepL":"";}
 static String summary(JSONArray rows){Map<String,Map<String,JSONObject>> pairs=new LinkedHashMap<>();for(int i=0;i<rows.length();i++){JSONObject r=rows.optJSONObject(i);if(r==null||r.optBoolean("reviewed")||r.optLong("translation_ms")<=0)continue;String engine=engine(r.optString("engine"));if(engine.isEmpty())continue;String key=GameNames.key(r.optString("game"))+"\n"+dev.arabicgames.SubtitleGate.fingerprint(r.optString("source"));pairs.computeIfAbsent(key,k->new HashMap<>()).put(engine,r);}
  List<JSONObject> quick=new ArrayList<>(),deep=new ArrayList<>();for(Map<String,JSONObject> p:pairs.values())if(p.containsKey("QuickMT")&&p.containsKey("DeepL")){quick.add(p.get("QuickMT"));deep.add(p.get("DeepL"));}
  if(quick.isEmpty())return "لا توجد جمل مشتركة كافية للمقارنة بين QuickMT وDeepL. أعد نفس حوار اللعبة بالمحركين مع تفعيل السجل، ثم قيّم صحة النتائج. نستبعد الذاكرة والحزم والنتائج المعدلة يدويًا حتى لا تبدو أسرع كمحرك. لا نرسل أي طلب مقارنة أثناء اللعب.";
  return "جمل مشتركة: "+quick.size()+" — نفس اللعبة والنص.\n"+stats("QuickMT",quick)+"\n"+stats("DeepL",deep)+"\nالدقة حسب تقييمك فقط؛ غير المقيّم لا يُعد صحيحًا. الزمن داخل التطبيق ولا يشمل بث اللعبة أو التلفزيون. لا نختار محركًا تلقائيًا من عينة صغيرة.";
 }
 private static String stats(String name,List<JSONObject> rows){List<Long> ms=new ArrayList<>();int good=0,bad=0;for(JSONObject r:rows){ms.add(r.optLong("total_ms"));if(r.optInt("rating")==1)good++;if(r.optInt("rating")==-1)bad++;}Collections.sort(ms);double median=ms.size()%2==1?ms.get(ms.size()/2):(ms.get(ms.size()/2-1)+ms.get(ms.size()/2))/2.0;return name+": وسيط التجهيز "+median+" ms؛ صحيح "+good+" / خطأ "+bad+" / غير مقيّم "+(rows.size()-good-bad);}
}
