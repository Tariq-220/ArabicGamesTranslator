namespace GameTranslator.Translation.Tests;
public class EngineComparisonTests {
 [Fact]public void DoesNotCompareDifferentDialogueOrGames(){var rows=new[]{new EngineComparison.Entry("GTA6","QuickMT","Go!","هيا",300,250),new EngineComparison.Entry("GTA6","DeepL","Stay!","ابق",900,850),new EngineComparison.Entry("RDR2","DeepL","Go!","هيا",100,50)};Assert.Contains("لا توجد جمل مشتركة",EngineComparison.Summary(rows));}
 [Fact]public void MatchedComparisonExcludesCacheAndShowsUnratedSeparately(){var rows=new[]{new EngineComparison.Entry("GTA6","QuickMT","Go now!","هيا",400,350,1),new EngineComparison.Entry("GTA 6","DeepL","Go now.","اذهب",800,750),new EngineComparison.Entry("GTA6","DeepL","Go now!","ذاكرة",1,0,-1)};var report=EngineComparison.Summary(rows);Assert.Contains("جمل مشتركة: 1",report);Assert.Contains("400",report);Assert.Contains("800",report);Assert.Contains("غير مقيّم 1",report);Assert.DoesNotContain("خطأ 1",report);}
}
