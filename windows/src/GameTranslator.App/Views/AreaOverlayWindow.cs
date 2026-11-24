// SPDX-License-Identifier: AGPL-3.0-only
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using GameTranslator.Core;
namespace GameTranslator.App.Views;

/// <summary>Non-activating, click-through regional output; lifecycle independent of dialogue.</summary>
internal sealed class AreaOverlayWindow : Window
{
    private static readonly Dictionary<string,AreaOverlayWindow> windows=new(StringComparer.Ordinal);
    private readonly TextBlock caption=new(){TextWrapping=TextWrapping.Wrap,TextAlignment=TextAlignment.Right,FlowDirection=FlowDirection.RightToLeft,
        Foreground=Brushes.White,FontFamily=new FontFamily("pack://application:,,,/GameTranslator;component/Assets/Fonts/#Noto Sans Arabic"),
        Effect=new DropShadowEffect{Color=Colors.Black,BlurRadius=3,ShadowDepth=1,Opacity=1}};
    private readonly Border border=new(){Padding=new Thickness(6),CornerRadius=new CornerRadius(5)};
    private readonly DispatcherTimer expiry=new();
    private long revision=-1;
    private AreaOverlayWindow()
    {
        AllowsTransparency=true;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;ShowActivated=false;ShowInTaskbar=false;Topmost=true;Focusable=false;
        Background=Brushes.Transparent;border.Child=caption;Content=border;
        SourceInitialized+=(_,_)=>{var handle=new WindowInteropHelper(this).Handle;SetWindowLong(handle,-20,GetWindowLong(handle,-20)|0x20|0x08000000|0x80);};
        expiry.Tick+=(_,_)=>{expiry.Stop();Hide();};
    }
    public static void Apply(AreaCaption value)
    {
        if(!DesktopSubtitleOverlay.IsOverlayEnabled)return;
        if(!windows.TryGetValue(value.Id,out var window)){if(windows.Count>=8)return;window=new AreaOverlayWindow();windows.Add(value.Id,window);}
        if(value.Revision<window.revision)return;window.revision=value.Revision;
        window.expiry.Stop();if(value.Clear){window.Hide();return;}
        var bounds=DesktopSubtitleOverlay.CaptionBounds;var box=value.Output;
        window.Left=bounds.Left+bounds.Width*box.Left;window.Top=bounds.Top+bounds.Height*box.Top;
        window.Width=Math.Max(60,bounds.Width*(box.Right-box.Left));window.Height=Math.Max(30,bounds.Height*(box.Bottom-box.Top));
        window.caption.Text=value.Text;window.caption.FontSize=value.FontSize;
        window.border.Background=new SolidColorBrush(Color.FromArgb((byte)(Math.Clamp(value.Background,0,100)*255/100),0,0,0));
        window.Show();window.expiry.Interval=TimeSpan.FromMilliseconds(value.DurationMs);window.expiry.Start();
    }
    public static void ClearAll(){foreach(var window in windows.Values){window.expiry.Stop();window.Close();}windows.Clear();}
    [DllImport("user32.dll")]private static extern int GetWindowLong(IntPtr window,int index);
    [DllImport("user32.dll")]private static extern int SetWindowLong(IntPtr window,int index,int value);
}
