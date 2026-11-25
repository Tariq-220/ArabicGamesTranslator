// SPDX-License-Identifier: AGPL-3.0-only
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using GameTranslator.Core;
using GameTranslator.App.Controls;
namespace GameTranslator.App.Views;

/// <summary>Content-sized caption, bounded by its output region; never draws a screen-wide background.</summary>
public sealed class DesktopSubtitleOverlay : Window
{
    private static DesktopSubtitleOverlay? instance;
    private static bool enabled,positioning;private static Rect? target;private static Point? dragged;
    private readonly DispatcherTimer timer=new();
    public static Rect CaptionBounds=>target??new Rect(SystemParameters.WorkArea.Left,SystemParameters.WorkArea.Top,SystemParameters.WorkArea.Width,SystemParameters.WorkArea.Height);
    public static bool IsOverlayEnabled{get=>enabled;set{enabled=value;if(!value)Clear();}}
    public static void SetTargetMonitor(int left,int top,int width,int height)=>target=new(left,top,width,height);
    public static void Initialize(){if(Application.Current is {} app)app.Dispatcher.Invoke(()=>instance??=new());}
    private DesktopSubtitleOverlay(){WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=Brushes.Transparent;Topmost=true;ShowActivated=false;ShowInTaskbar=false;Focusable=false;SizeToContent=SizeToContent.WidthAndHeight;
        SourceInitialized+=(_,_)=>ClickThrough(true);timer.Tick+=(_,_)=>{timer.Stop();if(!positioning)Hide();};
        MouseLeftButtonDown+=(_,e)=>{if(!positioning)return;if(e.ClickCount==2){positioning=false;dragged=new(Left,Top);ClickThrough(true);Hide();}else{DragMove();dragged=new(Left,Top);}};
    }
    public static void Display(string text,int durationMs=4500,SubtitleStyle? style=null,GameTranslator.Core.TextBox? box=null,double? lineHeight=null)
    {
        if(!enabled||string.IsNullOrWhiteSpace(text)||Application.Current is not {} app)return;
        app.Dispatcher.BeginInvoke(()=>{instance??=new();if(!positioning)instance.Render(text,durationMs,style??new(),box,lineHeight);});
    }
    private void Render(string text,int duration,SubtitleStyle style,GameTranslator.Core.TextBox? box,double? lineHeight=null)
    {
        var bounds=CaptionBounds;var a=(style.Appearance??new()).Validate();double size=CaptionSizing.FontPixels(style.FontSize,bounds.Width,bounds.Height,a.MatchSize,lineHeight);
        var b=box??new GameTranslator.Core.TextBox(.1,.74,.9,.9);
        var visual=CaptionVisual.Text(text,a,size,style.Background,style.Bold);visual.MaxWidth=Math.Max(100,(b.Right-b.Left)*bounds.Width);visual.Measure(new Size(visual.MaxWidth,double.PositiveInfinity));
        if(a.Position=="cover"&&a.CoverMinimum){visual.MinWidth=(b.Right-b.Left)*bounds.Width;visual.MinHeight=(b.Bottom-b.Top)*bounds.Height;visual.Measure(new Size(visual.MaxWidth,double.PositiveInfinity));}
        var width=Math.Min(visual.MaxWidth,visual.DesiredSize.Width);var height=visual.DesiredSize.Height;
        Content=visual;
        double x=b.Left*bounds.Width,y=b.Top*bounds.Height,availableW=(b.Right-b.Left)*bounds.Width,availableH=(b.Bottom-b.Top)*bounds.Height;
        x+=a.Align=="left"?0:a.Align=="right"?availableW-width:(availableW-width)/2;
        y=a.Position switch{"above"=>y-height-6,"below"=>b.Bottom*bounds.Height+6,"cover"=>y+(availableH-height)/2,_=>y+(a.Vertical=="top"?0:a.Vertical=="center"?(availableH-height)/2:availableH-height)};
        Left=dragged?.X??bounds.Left+Math.Clamp(x,0,Math.Max(0,bounds.Width-width));Top=dragged?.Y??bounds.Top+Math.Clamp(y,0,Math.Max(0,bounds.Height-height));
        Show();if(a.FadeMs>0)visual.BeginAnimation(UIElement.OpacityProperty,new System.Windows.Media.Animation.DoubleAnimation(0,1,TimeSpan.FromMilliseconds(a.FadeMs)));timer.Stop();timer.Interval=TimeSpan.FromMilliseconds(Math.Max(250,duration));timer.Start();
    }
    public static void Cover(GameTranslator.Core.TextBox box){
        if(!enabled||Application.Current is not {} app)return;app.Dispatcher.BeginInvoke(()=>{instance??=new();var bounds=CaptionBounds;instance.Content=new Border{Width=Math.Max(1,(box.Right-box.Left)*bounds.Width),Height=Math.Max(1,(box.Bottom-box.Top)*bounds.Height),Background=Brushes.Black};instance.Left=bounds.Left+box.Left*bounds.Width;instance.Top=bounds.Top+box.Top*bounds.Height;instance.Show();instance.timer.Stop();instance.timer.Interval=TimeSpan.FromSeconds(5);instance.timer.Start();});
    }
    public static void Clear(){Application.Current?.Dispatcher.BeginInvoke(()=>{instance?.timer.Stop();instance?.Hide();});}
    public static void ResetPosition(){dragged=null;}
    public static void StartDragMode(){if(Application.Current is not {} app)return;app.Dispatcher.Invoke(()=>{instance??=new();instance.Render("اسحب الترجمة إلى موضعها · انقر مرتين للتثبيت",30000,new(Background:75),null);positioning=true;instance.timer.Stop();instance.ClickThrough(false);instance.Cursor=Cursors.SizeAll;});}
    private void ClickThrough(bool on){var h=new WindowInteropHelper(this).Handle;SetWindowDisplayAffinity(h,0x11);int style=GetWindowLong(h,-20)|0x80000|0x80;SetWindowLong(h,-20,on?style|0x20|0x08000000:style&~0x20&~0x08000000);}
    [DllImport("user32.dll")]private static extern int GetWindowLong(IntPtr h,int n);
    [DllImport("user32.dll")]private static extern int SetWindowLong(IntPtr h,int n,int v);
    [DllImport("user32.dll")]private static extern bool SetWindowDisplayAffinity(IntPtr h,uint v);
}
