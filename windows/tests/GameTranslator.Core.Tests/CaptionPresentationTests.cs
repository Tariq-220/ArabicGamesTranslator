// SPDX-License-Identifier: AGPL-3.0-only
using GameTranslator.Core;
namespace GameTranslator.Core.Tests;
public class CaptionPresentationTests
{
 [Fact] public void InvalidAppearanceIsBoundedWithoutTouchingTranslation(){var a=new CaptionAppearance(Font:"remote-font",Backdrop:"html",SpeakerColor:"javascript:",FadeMs:900).Validate();Assert.Equal("noto",a.Font);Assert.Equal("game",a.Backdrop);Assert.Equal("auto",a.SpeakerColor);Assert.Equal(300,a.FadeMs);}
 [Fact] public void SpeakerNameHasItsOwnColourAndBodyIsUnchanged(){var t="لوسيا: افتح الباب.";var (name,body)=CaptionAppearance.SplitSpeaker(t);Assert.Equal("لوسيا:",name);Assert.Equal("افتح الباب.",body);Assert.Equal("#F39ABA",CaptionAppearance.SpeakerAccent(t,new()));Assert.Equal("#FFFFFF",CaptionAppearance.SpeakerAccent(t,new(SpeakerColors:false)));}
 [Fact] public void LayoutRoundTripIncludesNumbersAndStylesButNoAccountSettings(){var a=ReadingArea.Phone() with {Appearance=new(Font:"tajawal",Backdrop:"rounded")};var json=new ReadingLayout(1,[new("dialogue","الحوار",ReadingAreaKind.Dialogue,new(),new()),a]).Export();var layout=ReadingLayout.Import(json);Assert.Equal(a.Source,layout.Areas[1].Source);Assert.Equal("tajawal",layout.Areas[1].Appearance!.Font);Assert.DoesNotContain("api",json,StringComparison.OrdinalIgnoreCase);Assert.Throws<ArgumentException>(()=>ReadingLayout.Import(new ReadingLayout(9,layout.Areas).Export()));}
 [Fact] public void SeparateMessagesRetainGeometryAndBlueVersusWhite(){var f=Frame.Blank(300,600);Fill(f,0,0,300,600,240,240,240);Fill(f,80,350,200,110,40,90,230);var reading=new Reading([new("Kenton",new(.35,.02,.65,.06)),new("Dropped off scuba to your boat",new(.1,.2,.9,.24)),new("man. Happy hunting!",new(.1,.245,.8,.285)),new("Yeah, I'm on it man.",new(.32,.6,.9,.65))]);var panel=SpatialText.Build(f,reading,ReadingAreaKind.Phone);Assert.Equal(3,panel.Cells.Count);Assert.Contains("Happy hunting",panel.Cells[1].Source);Assert.NotEqual(panel.Cells[1].Background,panel.Cells[2].Background);Assert.True(panel.Cells[2].Bubble);Assert.Equal("#FFFFFF",panel.Cells[2].Foreground);Assert.StartsWith("iVBOR",panel.Image);Assert.True(Convert.FromBase64String(panel.Image!).Length<40000);}
 [Fact] public void MenuEntriesAreNotJoinedAcrossRowsOrColumns(){var f=Frame.Blank(500,500);var r=new Reading([new("Map",new(.1,.1,.2,.14)),new("Game",new(.1,.17,.2,.21)),new("Different panel",new(.6,.1,.9,.14))]);var p=SpatialText.Build(f,r,ReadingAreaKind.Menu);Assert.Equal(3,p.Cells.Count);Assert.Null(p.Image);}
 [Fact] public void PhoneDetectionRejectsBlankSceneryAndFindsPhoneInsideRoi(){var f=Frame.Blank(400,600);Assert.Null(SpatialText.FindPhone(f));Fill(f,120,80,150,450,220,220,220);var box=SpatialText.FindPhone(f);Assert.NotNull(box);Assert.InRange(box!.Left,.27,.33);Assert.InRange(box.Top,.1,.16);}
 [Fact] public void ColouredLabelIsJoinedToItsBodyAndIsNotAnUtteranceOnItsOwn(){
  var r=new Reading([new("Tourist:",new(.1,.2,.3,.25)),new("You two go tweak in the woods or something,",new(.31,.2,.9,.25)),new("not out here.",new(.3,.26,.65,.31))]);
  var merged=SpeakerMerge.AttachStandaloneLabels(r);Assert.Equal(2,merged.Lines.Count);Assert.StartsWith("Tourist: You two",merged.Text);
  Assert.Empty(SpeakerMerge.AttachStandaloneLabels(new Reading([new("Jason:",new(.1,.2,.2,.3))])).Lines);
  Assert.True(SpeakerMerge.HasBody(r));Assert.False(SpeakerMerge.HasBody(new Reading([new("Tourist:")])));
 }
 private static void Fill(Frame f,int x,int y,int width,int height,byte r,byte g,byte b){for(int yy=y;yy<y+height;yy++)for(int xx=x;xx<x+width;xx++){int i=yy*f.Stride+xx*4;f.Pixels[i]=b;f.Pixels[i+1]=g;f.Pixels[i+2]=r;f.Pixels[i+3]=255;}}
}
