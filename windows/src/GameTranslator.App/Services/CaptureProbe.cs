// SPDX-License-Identifier: AGPL-3.0-only
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using GameTranslator.Core;
namespace GameTranslator.App.Services;

/// <summary>Captures a real changing WPF window. Unsupported WGC is recorded explicitly.</summary>
internal static class CaptureProbe
{
    public static async void Run(Application app,string folder)
    {
        app.ShutdownMode=ShutdownMode.OnExplicitShutdown;Directory.CreateDirectory(folder);Window? window=null;
        try{
            window=new Window{Title="ArabicGames capture validation",Width=360,Height=240,Left=40,Top=40,Background=Brushes.Red,WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,ShowActivated=false};
            window.Show();window.UpdateLayout();var handle=new WindowInteropHelper(window).Handle;
            // Change inside the configured interval, then stop drawing. The new static image
            // must still arrive, even if no subsequent FrameArrived event rescues a dropped update.
            using var modern=GraphicsWindowCapture.TryStart(handle,false,()=>250);
            var red=modern is null?null:await WaitFor(modern,f=>Dominant(f,2));
            if(modern is not null&&red is null)throw new InvalidOperationException("WGC started but did not deliver the red window.");
            window.Background=Brushes.Blue;window.UpdateLayout();
            var blue=modern is null?null:await WaitFor(modern,f=>!ReferenceEquals(f,red)&&Dominant(f,0));
            if(modern is not null&&blue is null)throw new InvalidOperationException("WGC retained an old image after the window changed.");
            if(blue is not null&&(blue.Width<300||blue.Height<180))throw new InvalidOperationException("WGC window geometry is invalid.");
            window.Width=500;window.Height=300;window.UpdateLayout();
            var resized=modern is null?null:await WaitFor(modern,f=>f.Width>=499&&f.Height>=299&&Dominant(f,0));
            if(modern is not null&&resized is null)throw new InvalidOperationException($"WGC did not recreate its pool after a native window resize. Window={window.ActualWidth}x{window.ActualHeight}; {modern.Diagnostics}");
            var fallback=WindowCaptureService.Capture(handle);
            if(fallback is null||!Dominant(fallback,0))throw new InvalidOperationException("Compatibility capture did not deliver the blue window.");
            var streamResult="";WindowCaptureService.RegisterInternalPlayer(handle);var previousInterval=DesktopCaptureStreamSession.SamplingIntervalMs;
            try{
                DesktopCaptureStreamSession.SamplingIntervalMs=40;
                using var stream=new DesktopCaptureStreamSession("window:"+window.Title);
                var initial=new TaskCompletionSource<Frame>(TaskCreationOptions.RunContinuationsAsynchronously);var changed=new TaskCompletionSource<Frame>(TaskCreationOptions.RunContinuationsAsynchronously);
                stream.FrameReceived+=f=>{if(Dominant(f,0))initial.TrySetResult(f);if(Dominant(f,1))changed.TrySetResult(f);};
                stream.Start();await initial.Task.WaitAsync(TimeSpan.FromSeconds(5));window.Background=Brushes.Lime;window.UpdateLayout();
                var delivered=await changed.Task.WaitAsync(TimeSpan.FromSeconds(5));streamResult=delivered.CaptureBackend??"missing";
                if(modern is not null&&streamResult!="wgc")throw new InvalidOperationException("The live source did not use the verified WGC path.");
            }finally{DesktopCaptureStreamSession.SamplingIntervalMs=previousInterval;WindowCaptureService.UnregisterInternalPlayer(handle);}
            File.WriteAllText(Path.Combine(folder,"capture-probe.json"),JsonSerializer.Serialize(new{Wgc=modern is null?"unavailable_on_runner":"passed_changing_and_resized_native_window",Fallback="passed",LiveStream=streamResult,Width=resized?.Width,Height=resized?.Height,CopyMs=resized?.CaptureMs},new JsonSerializerOptions{WriteIndented=true}));
            window.Close();app.Shutdown(0);
        }catch(Exception e){File.WriteAllText(Path.Combine(folder,"errors.txt"),e.ToString());window?.Close();app.Shutdown(1);}
    }
    private static async Task<Frame?> WaitFor(GraphicsWindowCapture capture,Func<Frame,bool> valid)
    {
        var started=Environment.TickCount64;
        while(Environment.TickCount64-started<5000){if(capture.Faulted)throw new InvalidOperationException("WGC failed while copying the native surface. "+capture.Diagnostics+"\n"+capture.LastFault);if(capture.Latest is {} frame&&valid(frame))return frame;await Task.Delay(40);}
        return null;
    }
    private static bool Dominant(Frame frame,int channel)
    {
        int offset=frame.Height/2*frame.Stride+frame.Width/2*4;
        return frame.Pixels[offset+channel]>200&&frame.Pixels[offset+(channel==0?2:0)]<40;
    }
}
