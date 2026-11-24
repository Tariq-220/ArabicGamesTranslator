using GameTranslator.Core;
namespace GameTranslator.Translation.Tests;
public class DialogueReadingRegressionTests {
 private sealed class Reader(params string[] text):ITextReader {private readonly Queue<string> rows=new(text);public string Read(Frame image)=>rows.Count>0?rows.Dequeue():"";}
 private sealed class Translator:ITextTranslator {public TaskCompletionSource Started=new(TaskCreationOptions.RunContinuationsAsynchronously);public TaskCompletionSource Release=new(TaskCreationOptions.RunContinuationsAsynchronously);public bool Hold;public int Calls;public async Task<string> TranslateAsync(string text,CancellationToken cancellationToken=default){Calls++;Started.TrySetResult();if(Hold)await Release.Task.WaitAsync(cancellationToken);return "ينبغي أن نواصل السير.";}}
 private sealed class Sender:ISubtitleSender {public List<string> Lines=[];public void SendSubtitle(string text,int durationMs=15000,TextBox? box=null,bool paragraphs=false,bool replaces=false)=>Lines.Add(text);public void Clear(){}}
 [Fact]public async Task DialogueWaitsForACompleteStableReadingWithoutBlockingTheReader(){
  long time=0;var sender=new Sender();var translator=new Translator();var pipeline=new SubtitlePipeline(new Reader("We should keep moving.","We should keep moving."),translator,sender,clock:()=>time);
  DialogueReadingPolicy.Configure(pipeline,true,"dialogue");await pipeline.ProcessAsync(Frame.Blank(64,64),new(0,1,0,1));Assert.Empty(sender.Lines);
  time=120;await pipeline.ProcessAsync(Frame.Blank(64,64),new(0,1,0,1));await pipeline.WaitForTranslationsAsync();Assert.Single(sender.Lines);Assert.Equal(1,translator.Calls);
 }
 [Fact]public async Task SlightlyLateResultSurvivesAnOcrGap(){
  long time=0;var sender=new Sender();var translator=new Translator{Hold=true};var pipeline=new SubtitlePipeline(new Reader("We should keep moving.","We should keep moving.","",""),translator,sender,clock:()=>time);
  DialogueReadingPolicy.Configure(pipeline,true,"dialogue");await pipeline.ProcessAsync(Frame.Blank(64,64),new(0,1,0,1));time=120;await pipeline.ProcessAsync(Frame.Blank(64,64),new(0,1,0,1));
  await translator.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));time=300;await pipeline.ProcessAsync(Frame.Blank(64,64),new(0,1,0,1));time=1000;await pipeline.ProcessAsync(Frame.Blank(64,64),new(0,1,0,1));time=1150;translator.Release.TrySetResult();await pipeline.WaitForTranslationsAsync();Assert.Single(sender.Lines);
 }
 [Fact]public void SpeakerNameSurvivesTheWhiteTextPass(){var merged=SpeakerMerge.WithSpeakerFrom(new Reading([new("We should keep moving.",new(.2,.8,.6,.9))]),new Reading([new("Jason: We should keep moving.",new(.1,.8,.6,.9))]));Assert.Equal("Jason: We should keep moving.",merged.Text);}
 [Fact]public void PunctuationAfterDialogueCannotCrashTheRereadGuard(){var guard=new RereadGuard();guard.Filter("We should keep moving right now.",0);Assert.Equal("---",guard.Filter("---",50));}
}
