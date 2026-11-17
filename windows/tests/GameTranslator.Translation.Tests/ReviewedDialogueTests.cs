using GameTranslator.Core;
namespace GameTranslator.Translation.Tests;
public class ReviewedDialogueTests {
 [Theory][InlineData("ماذا حدث للهولندية؟","ماذا حدث لداتش؟")][InlineData("الهولندي هنا","داتش هنا")][InlineData("تحدث مع الهولندية","تحدث مع داتش")]
 public void DutchNameHandlesArabicPrefixes(string before,string after)=>Assert.Equal(after,DialogueProfile.Arabic("What happened to Dutch?",before,"RDR2"));
 [Fact]public void ActualLanguageIsNotReplacedByCharacterName()=>Assert.Equal("هل تتحدث الهولندية؟",DialogueProfile.Arabic("Do you speak Dutch?","هل تتحدث الهولندية؟","RDR2"));
 [Fact]public void ReviewedGlossaryIsExplicitAndGameScoped(){Assert.Equal("اركب حصانك.",DialogueProfile.Reviewed("Mount your horse","RDR2"));Assert.Null(DialogueProfile.Reviewed("Mount your horse","Another game"));Assert.Null(DialogueProfile.Reviewed("A new unseen sentence.","RDR2"));}
 [Fact]public void OcrPunctuationFixPreservesPossessive(){Assert.Equal("Mr. Milton's friends?",DialogueProfile.English("Mr. Milton•s friends?","RDR2"));Assert.Equal("I do.",DialogueProfile.English("1 do.","RDR2"));}
}
