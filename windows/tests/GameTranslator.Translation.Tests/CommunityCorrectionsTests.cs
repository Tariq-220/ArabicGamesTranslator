using System.Text.Json;
namespace GameTranslator.Translation.Tests;
public class CommunityCorrectionsTests {
 [Fact]public void CorrectsOnlyReviewedExactLinesInTheirGame(){Assert.Equal("تابع التقدم عبر أشجار النخيل.",CommunityCorrections.Find("RDR2","Keep going. Through the palms."));Assert.Null(CommunityCorrections.Find("GTA VI","Keep going. Through the palms."));Assert.Null(CommunityCorrections.Find("RDR2","Keep going through palms maybe"));}
 [Fact]public void UnreviewedReportsCannotBecomeACorrection(){var text=JsonSerializer.Serialize(new{format="arabic-games-reviewed-v1",source="en",target="ar",entries=new[]{new{game="RDR2",source="Keep going.",arabic="تابع.",reviewed=false}}});Assert.Throws<InvalidDataException>(()=>CommunityCorrections.Parse(text));}
 [Theory][InlineData("GTA 6")][InlineData("GTA6")][InlineData("GTA VI")][InlineData("Grand Theft Auto VI")][InlineData("Grand Theft Auto 6")]
 public void GtaAliasesUseTheSharedReviewedPack(string game){Assert.Equal("الآن أطفئ المحرك وانخفض.",CommunityCorrections.Find(game,"Now kill the engine, get down."));Assert.Null(CommunityCorrections.Find("RDR2","Now kill the engine, get down."));}
 [Fact]public void OlderCachedPackCannotReplaceNewBundledCorrections(){Assert.False(CommunityCorrections.IsCurrentRevision(1,4));Assert.True(CommunityCorrections.IsCurrentRevision(5,4));Assert.True(CommunityCorrections.IsCurrentRevision(4,4));}
 [Fact]public void ExistingPersonalGtaAliasesArePreserved(){var saved=CommunityCorrections.NormalizePersonal(new(){["gta 6\nHello?"]="مرحبًا؟"});Assert.Equal("مرحبًا؟",saved["grand theft auto vi\nhello?"]);}
}
