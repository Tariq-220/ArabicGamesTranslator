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
            using var modern=GraphicsWindowCapture.TryStart(handle,false,()=>40);
            var red=modern is null?null:await WaitFor(modern,f=>Dominant(f,2));
            if(modern is not null&&red is null)throw new InvalidOperationException("WGC started but did not deliver the red window.");
            window.Background=Brushes.Blue;window.UpdateLayout();
            var blue=modern is null?null:await WaitFor(modern,f=>!ReferenceEquals(f,red)&&Dominant(f,0));
            if(modern is not null&&blue is null)throw new InvalidOperationException("WGC retained an old image after the window changed.");
            if(blue is not null&&(blue.Width<300||blue.Height<180))throw new InvalidOperationException("WGC window geometry is invalid.");
            var fallback=WindowCaptureService.Capture(handle);
            if(fallback is null||!Dominant(fallback,0))throw new InvalidOperationException("Compatibility capture did not deliver the blue window.");
            File.WriteAllText(Path.Combine(folder,"capture-probe.json"),JsonSerializer.Serialize(new{Wgc=modern is null?"unavailable_on_runner":"passed_changing_native_window",Fallback="passed",Width=blue?.Width,Height=blue?.Height,CopyMs=blue?.CaptureMs},new JsonSerializerOptions{WriteIndented=true}));
            window.Close();app.Shutdown(0);
        }catch(Exception e){File.WriteAllText(Path.Combine(folder,"errors.txt"),e.ToString());window?.Close();app.Shutdown(1);}
    }
    private static async Task<Frame?> WaitFor(GraphicsWindowCapture capture,Func<Frame,bool> valid)
    {
        var started=Environment.TickCount64;
        while(Environment.TickCount64-started<5000){if(capture.Faulted)throw new InvalidOperationException("WGC failed while copying the native surface.");if(capture.Latest is {} frame&&valid(frame))return frame;await Task.Delay(40);}
        return null;
    }
    private static bool Dominant(Frame frame,int channel)
    {
        int offset=frame.Height/2*frame.Stride+frame.Width/2*4;
        return frame.Pixels[offset+channel]>200&&frame.Pixels[offset+(channel==0?2:0)]<40;
    }
}
