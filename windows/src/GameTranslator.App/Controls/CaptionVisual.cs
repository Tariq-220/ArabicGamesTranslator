// SPDX-License-Identifier: AGPL-3.0-only
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using GameTranslator.Core;
namespace GameTranslator.App.Controls;

public static class CaptionVisual
{
    public static FontFamily Font(string? id)=>new("pack://application:,,,/GameTranslator;component/Assets/Fonts/#"+(id switch{"tajawal"=>"Tajawal","cairo"=>"Cairo","changa"=>"Changa","lalezar"=>"Lalezar",_=>"Noto Sans Arabic"}));
    public static Brush Brush(string color)=>new SolidColorBrush((Color)ColorConverter.ConvertFromString(CaptionAppearance.IsColor(color)?color:"#FFFFFF"));
    public static Border Text(string text,CaptionAppearance? appearance,double size,int opacity,bool bold=false)
    {
        var a=(appearance??new()).Validate();var split=CaptionAppearance.SplitSpeaker(text);
        var label=new TextBlock{FlowDirection=FlowDirection.RightToLeft,FontFamily=Font(a.Font),FontSize=size,FontWeight=bold?FontWeights.Bold:FontWeights.Medium,
            TextWrapping=TextWrapping.Wrap,TextAlignment=a.Align switch{"left"=>TextAlignment.Left,"right"=>TextAlignment.Right,_=>TextAlignment.Center},Foreground=Brushes.White,
            LineHeight=size*1.42,LineStackingStrategy=LineStackingStrategy.BlockLineHeight};
        bool first=true;foreach(var row in text.Split('\n')){if(!first)label.Inlines.Add(new LineBreak());first=false;var part=CaptionAppearance.SplitSpeaker(row);if(part.Name.Length>0)label.Inlines.Add(new Run(part.Name+" "){Foreground=Brush(CaptionAppearance.SpeakerAccent(row,a))});label.Inlines.Add(new Run(part.Body));}
        if(a.Outline)label.Effect=new DropShadowEffect{Color=Colors.Black,ShadowDepth=1.3,BlurRadius=3.5,Opacity=1};
        var alpha=a.Backdrop=="none"?0:Math.Clamp(opacity,0,100)*255/100;
        var result=new Border{Child=label,Padding=new Thickness(size*.45,size*.12,size*.45,size*.18),CornerRadius=new CornerRadius(a.Backdrop is "game" or "rectangle"?0:6),
            Background=new SolidColorBrush(Color.FromArgb((byte)alpha,8,10,16)),HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top};
        Decorate(result,a,opacity,size);return result;
    }

    public static void Decorate(Border border,CaptionAppearance a,int opacity,double size)
    {
        if(a.Backdrop=="game")border.Background=new SolidColorBrush(Color.FromArgb((byte)(Math.Clamp(opacity,0,100)*255/100),22,36,62));
        if(!a.Accent)return;
        border.CornerRadius=new CornerRadius(0);border.BorderThickness=new Thickness(Math.Max(1.5,size/16));
        var gradient=new LinearGradientBrush{StartPoint=new Point(0,.5),EndPoint=new Point(1,.5)};
        foreach(var (color,offset) in new[]{("#FF2D95",0d),("#FF7A45",.30),("#FFC94A",.50),("#B45CFF",.72),("#2FD6FF",1d)})gradient.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(color),offset));
        gradient.Opacity=a.AccentOpacity/100d;
        foreach(var stop in gradient.GradientStops){var c=stop.Color;byte gray=(byte)((c.R+c.G+c.B)/3);double k=a.AccentStrength/100d;stop.Color=Color.FromRgb((byte)(gray+(c.R-gray)*k),(byte)(gray+(c.G-gray)*k),(byte)(gray+(c.B-gray)*k));}
        border.BorderBrush=gradient;if(a.Backdrop=="none")border.Background=new SolidColorBrush(Color.FromArgb((byte)(Math.Clamp(opacity,0,100)*255/100),10,6,20));
    }
    public static FrameworkElement Panel(PanelLayout panel,ReadingArea area,double targetWidth=1920,double targetHeight=1080)
    {
        if(panel.Kind=="menu")area=area with{Appearance=(area.Appearance??new()) with{Backdrop="none",Outline=true}};
        bool phone=panel.Kind is "phone" or "notification";bool floating=panel.Kind=="phone"&&area.Appearance?.PhoneStyle=="floating";
        var o=area.PresentationOutput();double scale=Math.Max(.01,Math.Min((o.Right-o.Left)*targetWidth/panel.Width,(o.Bottom-o.Top)*targetHeight/panel.Height));
        var canvas=new Canvas{Width=panel.Width,Height=panel.Height,ClipToBounds=true,Background=phone&&!floating?Brushes.White:Brushes.Transparent};
        if(panel.Image is {Length:>0 and <1600000} encoded){try{using var stream=new MemoryStream(Convert.FromBase64String(encoded));var bmp=new BitmapImage();bmp.BeginInit();bmp.CacheOption=BitmapCacheOption.OnLoad;bmp.StreamSource=stream;bmp.EndInit();bmp.Freeze();canvas.Children.Add(new Image{Source=bmp,Width=panel.Width,Height=panel.Height,Stretch=Stretch.Fill});}catch(FormatException){}}
        foreach(var cell in panel.Cells)
        {
            if(string.IsNullOrWhiteSpace(cell.Text))continue;var box=cell.Box;
            double w=Math.Max(8,(box.Right-box.Left)*panel.Width),h=Math.Max(8,(box.Bottom-box.Top)*panel.Height);
            double max=area.Appearance?.MatchSize!=false?Math.Max(8,cell.LineHeight*panel.Height*.95):area.FontSize;
            var label=new TextBlock{Width=w,HorizontalAlignment=HorizontalAlignment.Stretch,Text=cell.Text,FontFamily=Font(area.Appearance?.Font),FlowDirection=cell.Text.Any(c=>c>='\u0620'&&c<='\u06ff')?FlowDirection.RightToLeft:FlowDirection.LeftToRight,TextWrapping=TextWrapping.Wrap,
                FontWeight=cell.Heading?FontWeights.Bold:FontWeights.Medium,TextAlignment=cell.Heading&&box.Right-box.Left<.55?TextAlignment.Center:area.Appearance?.Align=="center"?TextAlignment.Center:area.Appearance?.Align=="left"?TextAlignment.Left:TextAlignment.Right,VerticalAlignment=area.Appearance?.Vertical=="center"?VerticalAlignment.Center:area.Appearance?.Vertical=="bottom"?VerticalAlignment.Bottom:VerticalAlignment.Top,Foreground=phone||area.Appearance?.Backdrop=="source"?Brush(cell.Foreground):Brushes.White,FontSize=area.Appearance?.MatchSize!=false?max:area.FontSize*targetWidth/1920/scale,Padding=new Thickness(3,0,3,0)};
            // Fit within the source cell. A long translation must not escape over a neighbouring message.
            for(int i=0;i<16;i++){label.Measure(new Size(w,double.PositiveInfinity));if(label.DesiredSize.Height<=h||label.FontSize<=8)break;label.FontSize=Math.Max(8,label.FontSize-1);}
            var block=new Border{Width=w,Height=h,ClipToBounds=true,CornerRadius=new CornerRadius(cell.Bubble?Math.Min(10,h*.15):0),Background=phone||area.Appearance?.Backdrop=="source"?Brush(cell.Background):area.Appearance?.Backdrop=="none"?Brushes.Transparent:new SolidColorBrush(Color.FromArgb((byte)(area.Background*255/100),0,0,0)),Child=label};
            if(floating){var bg=(SolidColorBrush)block.Background;bg.Opacity=.88;if(cell.Heading){block.Background=new SolidColorBrush(Color.FromArgb(200,8,10,16));label.Foreground=Brushes.White;}}
            if(!phone&&area.Appearance?.Outline!=false)label.Effect=new DropShadowEffect{Color=Colors.Black,ShadowDepth=1,BlurRadius=3,Opacity=1};
            if(!phone)Decorate(block,area.Appearance??new(),area.Background,label.FontSize);
            Canvas.SetLeft(block,box.Left*panel.Width);Canvas.SetTop(block,box.Top*panel.Height);canvas.Children.Add(block);
        }
        return new Viewbox{Child=canvas,Stretch=Stretch.Uniform,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Top};
    }
}
