using System.Text.Json;
namespace GameTranslator.Translation.Tests;
public class CommunityCorrectionsTests {
 [Fact]public void CorrectsOnlyReviewedExactLinesInTheirGame(){Assert.Equal("تابع التقدم عبر أشجار النخيل.",CommunityCorrections.Find("RDR2","Keep going. Through the palms."));Assert.Null(CommunityCorrections.Find("GTA VI","Keep going. Through the palms."));Assert.Null(CommunityCorrections.Find("RDR2","Keep going through palms maybe"));}
 [Fact]public void UnreviewedReportsCannotBecomeACorrection(){var text=JsonSerializer.Serialize(new{format="arabic-games-reviewed-v1",source="en",target="ar",entries=new[]{new{game="RDR2",source="Keep going.",arabic="تابع.",reviewed=false}}});Assert.Throws<InvalidDataException>(()=>CommunityCorrections.Parse(text));}
}
