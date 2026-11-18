using GameTranslator.Core;
namespace GameTranslator.Translation.Tests;
public class ReadingModesTests {
 [Theory][InlineData("phone-right")][InlineData("phone-left")][InlineData("menus")][InlineData("dialogue")]
 public void PresetsStayInsideTheVideo(string mode){var r=ReadingModes.Region(mode);Assert.InRange(r.Top,0,1);Assert.InRange(r.Bottom,0,1);Assert.True(r.Top<r.Bottom);Assert.True(r.Left<r.Right);Assert.Equal(mode=="dialogue",ReadingModes.DialogueOnly(mode));}
 [Fact]public void PhoneAndMenuPresetsIncludeUpperMessages(){Assert.True(ReadingModes.Region("phone-right").Top<.2);Assert.True(ReadingModes.Region("phone-left").Right<.5);Assert.Equal(1,ReadingModes.Region("menus").Right);}
}
