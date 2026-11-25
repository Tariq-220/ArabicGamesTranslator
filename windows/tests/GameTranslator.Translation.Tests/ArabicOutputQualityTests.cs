// SPDX-License-Identifier: AGPL-3.0-only
using GameTranslator.Core;
namespace GameTranslator.Translation.Tests;
public class ArabicOutputQualityTests
{
    [Theory]
    [InlineData("Are you ready?", "هل أنت ready؟", false)]
    [InlineData("Move now.", "Move now.", false)]
    [InlineData("Call the FIB.", "اتصل بمكتب FIB.", true)]
    [InlineData("Come here.", "تعال إلى هنا.", true)]
    [InlineData("Move now.", "", false)]
    [InlineData("Stranger: Move now.","Stranger: تحرك الآن.",true)]
    [InlineData("Stranger: Move now.","Stranger: تحرك now.",false)]
    [InlineData("Move now.","Stranger: تحرك الآن.",false)]
    [InlineData("Move now.", "تحرك FIB", false)]
    [InlineData("Are you there?", "هل YOU هناك؟", false)]
    public void OnlyArabicWithGroundedCodesIsDisplayable(string source,string output,bool valid) =>
        Assert.Equal(valid,ArabicOutputQuality.Accepts(source,output));

    [Fact] public void VisualLineWrapIsNotASeparateSentence() =>
        Assert.Equal("You got all the weapons?", ArabicOutputQuality.DialogueInput("You got all\r\nthe weapons?"));
}

public class ArabicSpeakerTests
{
    [Fact]public void ProviderSpeakerLabelsAreLocalizedOnlyWhenGroundedInSource()
    {
        Assert.Equal("جيسون: هيا بنا.",ArabicGameNames.Localize("Jason: Let's go.","Jason: هيا بنا."));
        Assert.Equal("Jason: هيا بنا.",ArabicGameNames.Localize("Let's go.","Jason: هيا بنا."));
        Assert.False(ArabicOutputQuality.Accepts("Lucia: Are you ready?","Lucia: هل أنت ready؟"));
        Assert.True(ArabicOutputQuality.Accepts("Lucia: Are you ready?","Lucia: هل أنت مستعد؟"));
    }
}
