package dev.arabicgames.phone;
import org.json.*;import org.junit.*;import static org.junit.Assert.*;
public class EngineComparisonTest {
 private JSONObject row(String game,String engine,String source,int total,int translation,int rating)throws Exception{return new JSONObject().put("game",game).put("engine",engine).put("source",source).put("total_ms",total).put("translation_ms",translation).put("rating",rating);}
 @Test public void onlyMatchedGameAndDialogueCanBeCompared()throws Exception{JSONArray rows=new JSONArray().put(row("GTA6","QuickMT","Go!",300,250,1)).put(row("GTA6","DeepL","Stay!",700,650,0)).put(row("RDR2","DeepL","Go!",100,50,1));assertTrue(EngineComparison.summary(rows).contains("لا توجد جمل مشتركة"));}
 @Test public void excludesCachedRowsAndDoesNotInventAccuracy()throws Exception{JSONArray rows=new JSONArray().put(row("GTA6","QuickMT","Go now!",400,350,1)).put(row("GTA 6","DeepL","Go now.",800,750,0)).put(row("GTA6","ذاكرة الترجمة — DeepL","Go now!",1,0,-1));String report=EngineComparison.summary(rows);assertTrue(report.contains("جمل مشتركة: 1"));assertTrue(report.contains("400"));assertTrue(report.contains("800"));assertTrue(report.contains("غير مقيّم 1"));assertFalse(report.contains("خطأ 1"));}
 @Test public void manuallyCorrectedRowsCannotCountAsModelAccuracy()throws Exception{JSONArray rows=new JSONArray().put(row("GTA6","QuickMT","Go now!",400,350,1).put("reviewed",true)).put(row("GTA6","DeepL","Go now!",800,750,1));assertTrue(EngineComparison.summary(rows).contains("لا توجد جمل مشتركة"));}
}
