// SPDX-License-Identifier: AGPL-3.0-only
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using GameTranslator.App.Controls;
using GameTranslator.Core;
using GameTranslator.Shell;
using Frame=GameTranslator.Core.Frame;
using TextBox=GameTranslator.Core.TextBox;
namespace GameTranslator.App.Views;

/// <summary>Native PS5 frames and the same caption visuals used in the region editor, without capturing our window.</summary>
public sealed class PlayStationPlayerWindow:Window
{
    private readonly AppController controller;
    private readonly Canvas canvas=new(){Width=1280,Height=720,Background=Brushes.Black,ClipToBounds=true};
    private readonly Image image=new(){Width=1280,Height=720,Stretch=Stretch.Fill};
    private readonly TextBlock status=new(){Text="بانتظار فيديو PS5؛ افتح اللعبة من الجهاز.",Foreground=Brushes.White,Margin=new Thickness(8),TextWrapping=TextWrapping.Wrap};
    private readonly Dictionary<string,(FrameworkElement Visual,long Until)> captions=new();
    private readonly DispatcherTimer frames=new(){Interval=TimeSpan.FromMilliseconds(80)};
    private Frame? last;private WriteableBitmap? bitmap;private bool closed,busy;
    public PlayStationPlayerWindow(AppController controller,Action settings)
    {
        this.controller=controller;Title="PS5 — البث داخل مترجم الألعاب";Width=1100;Height=700;MinWidth=640;MinHeight=420;Background=Brushes.Black;Foreground=Brushes.White;
        var root=new DockPanel();var controls=new WrapPanel{FlowDirection=FlowDirection.RightToLeft,Background=new SolidColorBrush(Color.FromRgb(15,23,36))};
        void Button(string text,Action action){var b=new Button{Content=text,Padding=new Thickness(10),Margin=new Thickness(4),MinHeight=42};b.Click+=(_,_)=>action();controls.Children.Add(b);}
        Button("إعدادات الترجمة · البث مستمر",settings);
        Button("بدء الترجمة",async()=>{try{await controller.StartAsync();}catch{status.Text="تعذر بدء الترجمة؛ راجع الاتصال والمحرك من الإعدادات.";}});
        Button("إيقاف الترجمة وإبقاء الفيديو",()=>{controller.Stop();controller.StartSourcePreview();});
        Button("ملء الشاشة / استعادة",()=>WindowState=WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized);
        DockPanel.SetDock(controls,Dock.Top);root.Children.Add(controls);DockPanel.SetDock(status,Dock.Bottom);root.Children.Add(status);
        canvas.Children.Add(image);root.Children.Add(new Viewbox{Stretch=Stretch.Uniform,Child=canvas});Content=root;
        controller.LiveSubtitleShown+=Dialogue;controller.SubtitlesCleared+=ClearDialogue;controller.AreaCaptionShown+=Area;controller.AreasCleared+=ClearAreas;
        frames.Tick+=(_,_)=>Refresh();Loaded+=(_,_)=>frames.Start();Closed+=(_,_)=>{closed=true;frames.Stop();controller.LiveSubtitleShown-=Dialogue;controller.SubtitlesCleared-=ClearDialogue;controller.AreaCaptionShown-=Area;controller.AreasCleared-=ClearAreas;};
    }
    private async void Refresh()
    {
        if(!IsVisible||WindowState==WindowState.Minimized)return;
        foreach(var key in captions.Where(x=>Environment.TickCount64>=x.Value.Until).Select(x=>x.Key).ToArray())Remove(key);
        frames.Interval=TimeSpan.FromMilliseconds(1000d/controller.Timing.PreviewFps);
        if(busy||controller.LastFrame is not {} frame||ReferenceEquals(last,frame))return;last=frame;busy=true;
        try{
            var preview=controller.Timing.FullResolutionPreview?frame:await Task.Run(()=>frame.Fit(1280));if(closed)return;
            if(bitmap is null||bitmap.PixelWidth!=preview.Width||bitmap.PixelHeight!=preview.Height){bitmap=new(preview.Width,preview.Height,96,96,PixelFormats.Bgr32,null);image.Source=bitmap;}
            bitmap.WritePixels(new Int32Rect(0,0,preview.Width,preview.Height),preview.Pixels,preview.Stride,0);
            status.Text=controller.IsRunning?"PS5 متصل · الترجمة تعمل · يمكنك تعديل الإعدادات أثناء البث":"معاينة PS5 · اضغط بدء الترجمة عند الجاهزية";
        }catch{status.Text="بانتظار إطار الفيديو التالي من PS5.";}finally{busy=false;}
    }
    private void Remove(string id){if(captions.Remove(id,out var old))canvas.Children.Remove(old.Visual);}
    private void Put(string id,FrameworkElement visual,CropRegion box,int ms){Remove(id);visual.IsHitTestVisible=false;visual.Width=(box.Right-box.Left)*1280;visual.Height=(box.Bottom-box.Top)*720;Canvas.SetLeft(visual,box.Left*1280);Canvas.SetTop(visual,box.Top*720);canvas.Children.Add(visual);captions[id]=(visual,Environment.TickCount64+ms);}
    private void Area(AreaCaption c){if(!Dispatcher.CheckAccess()){Dispatcher.BeginInvoke(()=>Area(c));return;}if(c.Clear){Remove(c.Id);return;}var area=new ReadingArea(c.Id,c.Id,ReadingAreaKind.Menu,new(),c.Output,FontSize:c.FontSize,Background:c.Background,Appearance:c.Appearance);Put(c.Id,c.Panel is {} panel?CaptionVisual.Panel(panel,area):CaptionVisual.Text(c.Text,c.Appearance,c.FontSize*1280d/1920,c.Background),c.Output,c.DurationMs);}
    private void Dialogue(string text,int ms,TextBox? source){if(!Dispatcher.CheckAccess()){Dispatcher.BeginInvoke(()=>Dialogue(text,ms,source));return;}var style=controller.Settings.Style;var a=style.Appearance??new();var box=a.Position=="custom"?controller.DialogueOutput:source is {} b?new CropRegion(b.Top,b.Bottom,b.Left,b.Right):controller.Settings.Region;
        var visual=CaptionVisual.Text(text,a,CaptionSizing.FontPixels(style.FontSize,1280,720,a.MatchSize,controller.LastCaptionLineHeight),style.Background);visual.MaxWidth=(box.Right-box.Left)*1280;visual.Measure(new Size(visual.MaxWidth,double.PositiveInfinity));double h=Math.Min(.8,visual.DesiredSize.Height/720),y=a.Position switch{"above"=>box.Top-h-.006,"below"=>box.Bottom+.006,"cover"=>box.Top+(box.Bottom-box.Top-h)/2,_=>a.Vertical=="top"?box.Top:a.Vertical=="center"?box.Top+(box.Bottom-box.Top-h)/2:box.Bottom-h};
        y=Math.Clamp(y,0,1-h);Put("dialogue",visual,new(y,y+h,box.Left,box.Right),ms);
    }
    private void ClearDialogue(){if(!Dispatcher.CheckAccess()){Dispatcher.BeginInvoke(ClearDialogue);return;}Remove("dialogue");}
    private void ClearAreas(){if(!Dispatcher.CheckAccess()){Dispatcher.BeginInvoke(ClearAreas);return;}foreach(var key in captions.Keys.Where(x=>x!="dialogue").ToArray())Remove(key);}
}
