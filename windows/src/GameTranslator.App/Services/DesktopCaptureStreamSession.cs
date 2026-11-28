// SPDX-License-Identifier: AGPL-3.0-only

using GameTranslator.Core;
using GameTranslator.RemotePlay;

namespace GameTranslator.App.Services;

/// <summary>
/// Masaüstünü, seçili monitörü veya OBS / Pencereyi yakalayarak kareleri altyazı motoruna aktaran IStreamSession.
/// Capture Card kullanıcıları ve OBS Fullscreen Projector kullananlar için PS5 Remote Play gerektirmez.
/// </summary>
public sealed class DesktopCaptureStreamSession : IStreamSession
{
    private static int samplingInterval=80;
    public static int SamplingIntervalMs {get=>Volatile.Read(ref samplingInterval);set=>Volatile.Write(ref samplingInterval,Math.Clamp(value,16,1000));}
    public event Action<Frame>? FrameReceived;
    public event Action<SessionState>? StateChanged;
#pragma warning disable CS0067
    public event Action<RegisteredConsole>? ConsoleMoved;
#pragma warning restore CS0067

    private readonly string? targetWindowTitle;
    private readonly int screenIndex;
    private CancellationTokenSource? cts;
    private Task? loopTask;
    private bool running;

    [System.Runtime.InteropServices.DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
    private static extern uint TimeBeginPeriod(uint uMilliseconds);

    [System.Runtime.InteropServices.DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
    private static extern uint TimeEndPeriod(uint uMilliseconds);
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct NativePoint {public int X,Y;}
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(NativePoint point,uint flags);

    public DesktopCaptureStreamSession(string? targetWindowTitle = null, int screenIndex = 0)
    {
        this.targetWindowTitle = targetWindowTitle;
        this.screenIndex = screenIndex;
    }

    public void Start()
    {
        if (running) return;
        running = true;
        cts = new CancellationTokenSource();
        StateChanged?.Invoke(SessionState.Connecting);

        loopTask = Task.Run(() => CaptureLoopAsync(cts.Token));
    }

    private async Task CaptureLoopAsync(CancellationToken token)
    {
        int emittedStreaming=0;
        GraphicsWindowCapture? graphics=null;IntPtr graphicsTarget=IntPtr.Zero;bool graphicsMonitor=false,attempted=false;long graphicsStarted=0,lastCompatibilityCheck=0;
        void Emit(Frame frame){
            if(token.IsCancellationRequested||!running)return;
            if(Interlocked.Exchange(ref emittedStreaming,1)==0)StateChanged?.Invoke(SessionState.Streaming);
            FrameReceived?.Invoke(frame);
        }
        Frame? ModernFrame(IntPtr target,bool monitor,Func<Frame?> compatibility,out bool warming)
        {
            if(target!=graphicsTarget||monitor!=graphicsMonitor){graphics?.Dispose();graphics=null;graphicsTarget=target;graphicsMonitor=monitor;attempted=false;}
            if(!attempted){
                attempted=true;graphicsStarted=Environment.TickCount64;lastCompatibilityCheck=graphicsStarted;graphics=GraphicsWindowCapture.TryStart(target,monitor,()=>SamplingIntervalMs);
                if(graphics is {} owner)owner.FrameReady+=fresh=>{
                    if(!ReferenceEquals(owner,Volatile.Read(ref graphics)))return;
                    if(monitor&&WindowCaptureService.SetupUiIsForeground())return;
                    Emit(fresh);
                };
            }
            if(graphics?.Faulted==true){graphics.Dispose();graphics=null;}
            var latest=graphics?.Latest;
            if(latest is not null&&Environment.TickCount64-lastCompatibilityCheck>=1500&&WindowCaptureService.IsFrameBlack(latest.Pixels,latest.Width,latest.Height)){
                lastCompatibilityCheck=Environment.TickCount64;
                // A protected/incompatible WGC surface can be black without throwing. Switch only
                // if the existing capture can actually see content; genuine black loading screens stay black.
                var old=compatibility();
                if(old is not null&&!WindowCaptureService.IsFrameBlack(old.Pixels,old.Width,old.Height)){graphics?.Dispose();graphics=null;latest=old;}
            }
            warming=graphics is not null&&latest is null&&Environment.TickCount64-graphicsStarted<1200;
            if(graphics is not null&&latest is null&&!warming){graphics.Dispose();graphics=null;}
            return latest;
        }
        TimeBeginPeriod(1);

        try
        {
            while (running && !token.IsCancellationRequested)
            {
                var iterationStarted=Environment.TickCount64;
                try
                {
                    // A full-monitor capture must not feed the translator's own settings/status back into OCR.
                    if(targetWindowTitle?.StartsWith("monitor:",StringComparison.OrdinalIgnoreCase)==true&&WindowCaptureService.SetupUiIsForeground()){
                        await Task.Delay(80,token);continue;
                    }
                    Frame? frame = null;

                    // 1. Belirli bir fiziksel monitör seçilmişse o monitörün koordinatlarını yakala
                    if (!string.IsNullOrWhiteSpace(targetWindowTitle) && targetWindowTitle.StartsWith("monitor:", StringComparison.OrdinalIgnoreCase))
                    {
                        if (int.TryParse(targetWindowTitle["monitor:".Length..], out int mIndex))
                        {
                            var monitors = WindowCaptureService.GetDisplayMonitors();
                            var targetMon = monitors.FirstOrDefault(m => m.Index == mIndex) ?? monitors.FirstOrDefault(m => m.IsPrimary);
                            if (targetMon != null)
                            {
                                var monitor=MonitorFromPoint(new NativePoint{X=targetMon.Left+1,Y=targetMon.Top+1},0);
                                frame=ModernFrame(monitor,true,()=>WindowCaptureService.CaptureScreen(targetMon.Left,targetMon.Top,targetMon.Width,targetMon.Height),out var warming);
                                if(frame is null&&!warming)frame=WindowCaptureService.CaptureScreen(targetMon.Left, targetMon.Top, targetMon.Width, targetMon.Height);
                            }
                        }
                    }
                    // 2. Özel bir pencere seçilmişse (SADECE o pencere yakalanır, asla tüm ekrana taşmaz)
                    else if (!string.IsNullOrWhiteSpace(targetWindowTitle))
                    {
                        string winTitle = targetWindowTitle.StartsWith("window:", StringComparison.OrdinalIgnoreCase)
                            ? targetWindowTitle["window:".Length..]
                            : targetWindowTitle;

                        var hWnd = WindowCaptureService.FindWindowBySubstring(winTitle);
                        if (hWnd != IntPtr.Zero)
                        {
                            frame=ModernFrame(hWnd,false,()=>WindowCaptureService.Capture(hWnd),out var warming);
                            if(frame is null&&!warming)frame=WindowCaptureService.Capture(hWnd);
                        }
                    }
                    // 3. Özel bir pencere veya monitör belirtilmemişse genel masaüstünü yakala
                    else
                    {
                        frame = WindowCaptureService.CaptureScreen();
                    }

                    if (frame != null)
                    {
                        if(token.IsCancellationRequested)break;
                        if(frame.CaptureBackend is null)frame=new Frame(frame.Pixels,frame.Width,frame.Height,frame.Stride){CaptureBackend="gdi",CaptureMs=(int)Math.Clamp(Environment.TickCount64-iterationStarted,0,int.MaxValue)};
                        // New WGC images arrive immediately via FrameReady. Periodic repeats only
                        // confirm a static page; an in-progress fresh copy must not be replaced by an old image.
                        if(graphics is null||!graphics.Updating)Emit(frame);
                    }
                }
                catch { }

                try
                {
                    await Task.Delay(CaptureCadence.RemainingMs(SamplingIntervalMs,Environment.TickCount64-iterationStarted), token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
        finally
        {
            graphics?.Dispose();
            TimeEndPeriod(1);
        }
    }

    public void Stop()
    {
        if (!running) return;
        running = false;
        cts?.Cancel();
        StateChanged?.Invoke(SessionState.Disconnected(DisconnectKind.Stopped));
    }

    public void ReconnectNow()
    {
        if (!running) return;
        Stop();
        Start();
    }

    public void Dispose()
    {
        Stop();
        cts?.Dispose();
    }
}
