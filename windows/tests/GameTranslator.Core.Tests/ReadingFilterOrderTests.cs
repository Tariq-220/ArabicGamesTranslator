namespace GameTranslator.Core.Tests;
public class ReadingFilterOrderTests
{
 [Fact]public async Task FiltersBeforeSelectingBottomSubtitleBlock(){
  var raw=new Reading([new("Keep going.",new(.2,.6,.4,.7)),new("Rob Antagonize Greet",new(.8,.9,.98,.98))]);
  var translator=new FakeTranslator();var clock=new TestClock();
  var pipeline=new SubtitlePipeline(new LinesReader(raw),translator,new FakeSender(),clock:clock.Read){ReadingFilter=(reading,area)=>new Reading(reading.Lines.Where(l=>l.Text!="Rob Antagonize Greet"))};
  for(int i=0;i<3;i++){await pipeline.ProcessAsync(Frame.Blank(64,64),new(0,1,0,1));clock.Advance(80);}
  await pipeline.WaitForTranslationsAsync();
  Assert.Contains("Keep going.",translator.Requested);Assert.DoesNotContain("Rob Antagonize Greet",translator.Requested);
 }
}
