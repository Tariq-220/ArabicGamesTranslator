using GameTranslator.Core;
namespace GameTranslator.Core.Tests;
public class ReadingAreaTests_057
{
    [Fact]
    public void PhoneDetectionJitterIsSmoothedAndGraceExpires()
    {
        var tracker=new PhonePanelTracker();var original=new CropRegion(.1,.9,.4,.8);
        Assert.Equal(original,tracker.Locate(original,0));
        var moved=tracker.Locate(new(.12,.92,.42,.82),200)!;
        Assert.InRange(moved.Top,.104,.106);
        Assert.Equal(moved,tracker.Locate(null,1000));
        Assert.Null(tracker.Locate(null,1800));
        Assert.False(SpatialText.PhoneSurfacePresent(Frame.Blank(100,100),original));
    }
    [Fact]
    public void NormalMessagesUseOneFontDespiteOcrHeightJitter()
    {
        var panel=new PanelLayout(400,700,"phone",null,[new("First message.","رسالة أولى.",new(.1,.2,.9,.3),"#FFFFFF","#20232A",.018,true),new("Second message.","رسالة ثانية.",new(.1,.4,.9,.5),"#FFFFFF","#20232A",.028,true)]);
        var result=SpatialText.ReflowPhone(panel);
        Assert.Equal(result.Cells[0].LineHeight,result.Cells[1].LineHeight);
        Assert.Equal("#20232A",result.Cells[1].Foreground);
    }
    [Fact]
    public async Task PhoneTextIsNotAlsoRenderedAsGameDialogue()
    {
        long now=0;var phone=ReadingArea.Phone() with{AutoDetect=false,Source=new(.1,.9,.6,.95)};
        var reader=new PhoneReader();using var session=new ReadingAreaSession([phone],()=>reader,new FakeTranslator(),()=>now);
        await session.ProcessAsync(Frame.Blank(400,300));
        var text=new TextLine("Might help him with some hunting.",new(.65,.7,.9,.75));
        Assert.Empty(session.WithoutPhoneText(new([text]),new(0,0,1,1)).Lines);
        Assert.Single(session.WithoutPhoneText(new([text with{Box=new(.1,.7,.4,.75)}]),new(0,0,1,1)).Lines);
        now=2500;Assert.Single(session.WithoutPhoneText(new([text]),new(0,0,1,1)).Lines);
    }
    private sealed class PhoneReader:ITextReader{public string Read(Frame f)=>"";public Reading ReadSpatial(Frame f)=>new([new("Lucia",new(.3,.02,.6,.07)),new("Might help him with some hunting.",new(.1,.6,.9,.7))]);}
    [Fact]
    public void SpeakerLabelDoesNotReplaceTheSentenceAndShoutedNamesRemainValid()
    {
        Assert.Empty(SpeakerMerge.AttachStandaloneLabels(new([new("Billy",new(.1,.3,.2,.35))])).Lines);
        var joined=SpeakerMerge.AttachStandaloneLabels(new([new("Billy",new(.1,.3,.2,.35)),new("Now, you couldn't pay me to go.",new(.21,.3,.9,.35))]));
        Assert.Equal("Billy: Now, you couldn't pay me to go.",joined.Text);
        Assert.False(SpeakerMerge.HasBody(new([new("Billy")])));Assert.True(SpeakerMerge.HasBody(new([new("Billy!")])));
        Assert.False(ArabicOutputQuality.Accepts("Billy: Now, you couldn't pay me to go.","بيلي:"));Assert.True(ArabicOutputQuality.Accepts("Billy","بيلي"));
    }
    [Fact]
    public void ClippedTopMessageIsNotMistakenForASecondContactName()
    {
        var panel=SpatialText.Build(Frame.Blank(400,700),new([new("Lucia",new(.35,.02,.65,.05)),new("with it.",new(.3,.09,.8,.115))]),ReadingAreaKind.Phone);
        Assert.Single(panel.Cells,c=>c.Heading);Assert.True(panel.Cells.Single(c=>c.Source=="with it.").Bubble);
    }
}
