// SPDX-License-Identifier: AGPL-3.0-only
// Persistent player behavior adapted from VI Translator src-tauri/src/xbox.rs.
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using GameTranslator.App.Services;
using GameTranslator.Core;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
namespace GameTranslator.App.Views;

/// <summary>The app owns a dedicated Microsoft web player. Setup navigation never hides,
/// disposes or suspends this browser; capture may keep reading it while settings are in front.</summary>
public sealed class XboxPlayerWindow : Window
{
    public const string PlayerTitle="Xbox — ArabicGames";
    private readonly WebView2 browser=new();
    private readonly TextBlock message=new(){Text="جارٍ فتح Xbox الرسمي…",Margin=new Thickness(12)};
    private bool closing;
    private IntPtr playerHandle;
    public XboxPlayerWindow()
    {
        Title=PlayerTitle;Width=1100;Height=700;MinWidth=640;MinHeight=400;
        var root=new DockPanel();DockPanel.SetDock(message,Dock.Bottom);root.Children.Add(message);root.Children.Add(browser);Content=root;
        SourceInitialized+=(_,_)=>{playerHandle=new WindowInteropHelper(this).Handle;WindowCaptureService.RegisterInternalPlayer(playerHandle);};
        Loaded+=async(_,_)=>{try{await Open();}catch(Exception){message.Text="تعذر تشغيل WebView2. ثبّت Microsoft Edge WebView2 Runtime ثم أعد فتح المصدر.";}};
        // A minimized native window cannot be captured. Keep this player drawable during settings changes.
        StateChanged+=(_,_)=>{if(!closing&&WindowState==WindowState.Minimized)WindowState=WindowState.Normal;};
        Closed+=(_,_)=>{closing=true;WindowCaptureService.UnregisterInternalPlayer(playerHandle);browser.Dispose();};
    }
    private async Task Open()
    {
        var profile=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ArabicGames","XboxWeb");
        var options=new CoreWebView2EnvironmentOptions("--disable-features=CalculateNativeWinOcclusion --disable-backgrounding-occluded-windows --disable-renderer-backgrounding --disable-background-timer-throttling --autoplay-policy=no-user-gesture-required");
        var environment=await CoreWebView2Environment.CreateAsync(null,profile,options);
        if(closing)return;await browser.EnsureCoreWebView2Async(environment);if(closing)return;
        var web=browser.CoreWebView2;
        web.Settings.AreDevToolsEnabled=false;web.Settings.AreHostObjectsAllowed=false;web.Settings.IsWebMessageEnabled=false;
        web.NavigationStarting+=(_,e)=>{
            if(XboxNavigation.Allowed(e.Uri))return;e.Cancel=true;
            if(Uri.TryCreate(e.Uri,UriKind.Absolute,out var url)&&url.Scheme=="https")
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url.AbsoluteUri){UseShellExecute=true});
        };
        web.NewWindowRequested+=async(_,e)=>{
            e.Handled=true;if(e.Uri!="about:blank"&&!XboxNavigation.Allowed(e.Uri))return;
            var deferral=e.GetDeferral();
            var content=new WebView2();var popup=new Window{Owner=this,Title="تسجيل Microsoft — ArabicGames",Width=540,Height=720,Content=content};
            popup.Closed+=(_,_)=>content.Dispose();popup.Show();
            try{
                await content.EnsureCoreWebView2Async(environment);
                var auth=content.CoreWebView2;auth.Settings.AreHostObjectsAllowed=false;auth.Settings.IsWebMessageEnabled=false;auth.Settings.AreDevToolsEnabled=false;
                auth.NavigationStarting+=(_,navigation)=>{if(navigation.Uri!="about:blank"&&!XboxNavigation.Allowed(navigation.Uri))navigation.Cancel=true;};
                auth.NewWindowRequested+=(_,navigation)=>navigation.Handled=true;
                auth.WindowCloseRequested+=(_,_)=>popup.Close();
                e.NewWindow=auth;
            }catch{popup.Close();message.Text="تعذر فتح نافذة Microsoft. أغلق تسجيل الدخول وأعد المحاولة.";}
            finally{deferral.Complete();}
        };
        web.PermissionRequested+=(_,e)=>{
            // Translation consumes video only; microphone and camera are never required.
            if(e.PermissionKind is CoreWebView2PermissionKind.Microphone or CoreWebView2PermissionKind.Camera)e.State=CoreWebView2PermissionState.Deny;
        };
        await web.AddScriptToExecuteOnDocumentCreatedAsync("""
        (()=>{try {
          Object.defineProperty(Document.prototype,'hidden',{configurable:true,get:()=>false});
          Object.defineProperty(Document.prototype,'visibilityState',{configurable:true,get:()=> 'visible'});
          const quiet=e=>{if(e.target===window||e.target===document)e.stopImmediatePropagation();};
          for(const t of [window,document])for(const event of ['visibilitychange','blur','pagehide'])t.addEventListener(event,quiet,true);
        }catch(e){}})();
        """);
        web.Navigate("https://play.xbox.com/remoteplay");
        message.Text="سجّل الدخول في صفحة Microsoft الرسمية. اترك نافذة البث مفتوحة أثناء ضبط المناطق؛ صوت اللعب يبقى من التلفزيون.";
        web.IsMuted=true;
    }
}
