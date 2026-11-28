// SPDX-License-Identifier: AGPL-3.0-only
// Windows.Graphics.Capture desktop interop follows Microsoft's documented HWND/HMONITOR API.
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using GameTranslator.Core;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Imaging;
using WinRT;

namespace GameTranslator.App.Services;

/// <summary>Event-driven acquisition with a single latest-frame slot. No growing GPU/OCR queue.
/// Uses the existing Windows SDK projection; no model, runtime download or extra native DLL.</summary>
internal sealed class GraphicsWindowCapture : IDisposable
{
    private readonly IDirect3DDevice device;
    private readonly GraphicsCaptureItem item;
    private readonly Direct3D11CaptureFramePool pool;
    private readonly GraphicsCaptureSession session;
    private readonly Func<int> interval;
    private Frame? latest;
    private SizeInt32 size;
    private long lastCopy=long.MinValue/2;
    private int processing,disposed,released;
    public bool Faulted {get;private set;}
    public Frame? Latest=>Volatile.Read(ref latest);

    public static GraphicsWindowCapture? TryStart(IntPtr handle,bool monitor,Func<int> interval)
    {
        if(handle==IntPtr.Zero)return null;
        try{return GraphicsCaptureSession.IsSupported()?new(handle,monitor,interval):null;}
        catch(Exception e) when(e is COMException or InvalidOperationException or UnauthorizedAccessException or DllNotFoundException or TypeLoadException or ArgumentException){return null;}
    }
    private GraphicsWindowCapture(IntPtr handle,bool monitor,Func<int> interval)
    {
        this.interval=interval;
        device=CreateDevice();
        try{
            item=CreateItem(handle,monitor);size=item.Size;
            pool=Direct3D11CaptureFramePool.CreateFreeThreaded(device,DirectXPixelFormat.B8G8R8A8UIntNormalized,2,size);
            try{session=pool.CreateCaptureSession(item);}
            catch{pool.Dispose();throw;}
            try{
                session.IsCursorCaptureEnabled=false;
                pool.FrameArrived+=OnFrame;item.Closed+=OnClosed;session.StartCapture();
            }catch{pool.FrameArrived-=OnFrame;item.Closed-=OnClosed;session.Dispose();pool.Dispose();throw;}
        }catch{(device as IDisposable)?.Dispose();throw;}
    }
    private void OnClosed(GraphicsCaptureItem sender,object args)=>Faulted=true;
    private async void OnFrame(Direct3D11CaptureFramePool sender,object args)
    {
        if(Volatile.Read(ref disposed)!=0||Interlocked.Exchange(ref processing,1)!=0)return;
        try{
            if(Volatile.Read(ref disposed)!=0)return;
            // Prefer the newest completed frame after a slow copy. The two-slot GPU pool
            // must not become an extra queue of old subtitle images.
            var frame=sender.TryGetNextFrame();if(frame is null)return;
            try{
            for(int n=0;n<2;n++){var newer=sender.TryGetNextFrame();if(newer is null)break;frame.Dispose();frame=newer;}
            var at=Environment.TickCount64;
            if(at-lastCopy<interval())return;
            lastCopy=at;
            var content=frame.ContentSize;
            if(content.Width<1||content.Height<1)return;
            using var bitmap=await SoftwareBitmap.CreateCopyFromSurfaceAsync(frame.Surface,BitmapAlphaMode.Ignore);
            var pixels=new byte[checked(bitmap.PixelWidth*bitmap.PixelHeight*4)];bitmap.CopyToBuffer(pixels.AsBuffer());
            var captured=new Frame(pixels,bitmap.PixelWidth,bitmap.PixelHeight,bitmap.PixelWidth*4)
                {CaptureMs=(int)Math.Clamp(Environment.TickCount64-at,0,int.MaxValue),CaptureBackend="wgc"};
            // During a resize the pool can briefly have the previous surface dimensions.
            if(content.Width<captured.Width||content.Height<captured.Height)
                captured=captured.Crop(0,0,Math.Min(content.Width,captured.Width),Math.Min(content.Height,captured.Height));
            if(Volatile.Read(ref disposed)==0)Volatile.Write(ref latest,captured);
            }finally{frame.Dispose();}
            // Recreate only after releasing the borrowed surface.
            var next=item.Size;
            if(next.Width>0&&next.Height>0&&(next.Width!=size.Width||next.Height!=size.Height)&&Volatile.Read(ref disposed)==0){sender.Recreate(device,DirectXPixelFormat.B8G8R8A8UIntNormalized,2,next);size=next;}
        }
        catch(Exception){Faulted=true;}
        finally{Volatile.Write(ref processing,0);ReleaseIfStopped();}
    }
    public void Dispose()
    {
        if(Interlocked.Exchange(ref disposed,1)!=0)return;
        pool.FrameArrived-=OnFrame;item.Closed-=OnClosed;
        session.Dispose();Volatile.Write(ref latest,null);ReleaseIfStopped();
    }
    private void ReleaseIfStopped()
    {
        if(Volatile.Read(ref disposed)==0||Volatile.Read(ref processing)!=0||Interlocked.Exchange(ref released,1)!=0)return;
        pool.Dispose();(device as IDisposable)?.Dispose();
    }

    private static IDirect3DDevice CreateDevice()
    {
        IntPtr native=IntPtr.Zero,context=IntPtr.Zero,dxgi=IntPtr.Zero,inspectable=IntPtr.Zero;
        try{
            Marshal.ThrowExceptionForHR(D3D11CreateDevice(IntPtr.Zero,1,IntPtr.Zero,0x20,IntPtr.Zero,0,7,out native,out _,out context));
            var iid=new Guid("54EC77FA-1377-44E6-8C32-88FD5F44C84C");Marshal.ThrowExceptionForHR(Marshal.QueryInterface(native,ref iid,out dxgi));
            Marshal.ThrowExceptionForHR(CreateDirect3D11DeviceFromDXGIDevice(dxgi,out inspectable));
            return MarshalInterface<IDirect3DDevice>.FromAbi(inspectable);
        }finally{foreach(var ptr in new[]{inspectable,dxgi,context,native})if(ptr!=IntPtr.Zero)Marshal.Release(ptr);}
    }
    private static GraphicsCaptureItem CreateItem(IntPtr handle,bool monitor)
    {
        const string name="Windows.Graphics.Capture.GraphicsCaptureItem";
        IntPtr text=IntPtr.Zero,factory=IntPtr.Zero,result=IntPtr.Zero;object? rcw=null;
        try{
            Marshal.ThrowExceptionForHR(WindowsCreateString(name,name.Length,out text));
            var factoryId=typeof(IGraphicsCaptureItemInterop).GUID;Marshal.ThrowExceptionForHR(RoGetActivationFactory(text,ref factoryId,out factory));
            rcw=Marshal.GetObjectForIUnknown(factory);var interop=(IGraphicsCaptureItemInterop)rcw;
            var itemId=new Guid("79C3F95B-31F7-4EC2-A464-632EF5D30760");
            result=monitor?interop.CreateForMonitor(handle,ref itemId):interop.CreateForWindow(handle,ref itemId);
            return MarshalInspectable<GraphicsCaptureItem>.FromAbi(result);
        }finally{
            if(result!=IntPtr.Zero)Marshal.Release(result);
            if(rcw is not null)Marshal.ReleaseComObject(rcw);
            if(factory!=IntPtr.Zero)Marshal.Release(factory);
            if(text!=IntPtr.Zero)WindowsDeleteString(text);
        }
    }
    [ComImport,Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IGraphicsCaptureItemInterop
    {
        IntPtr CreateForWindow(IntPtr hwnd,ref Guid iid);
        IntPtr CreateForMonitor(IntPtr monitor,ref Guid iid);
    }
    [DllImport("d3d11.dll",ExactSpelling=true)]private static extern int D3D11CreateDevice(IntPtr adapter,int driverType,IntPtr software,uint flags,IntPtr levels,uint count,uint sdk,out IntPtr device,out int level,out IntPtr context);
    [DllImport("d3d11.dll",ExactSpelling=true)]private static extern int CreateDirect3D11DeviceFromDXGIDevice(IntPtr device,out IntPtr result);
    [DllImport("combase.dll",CharSet=CharSet.Unicode,ExactSpelling=true)]private static extern int WindowsCreateString(string value,int length,out IntPtr result);
    [DllImport("combase.dll",ExactSpelling=true)]private static extern int WindowsDeleteString(IntPtr value);
    [DllImport("combase.dll",ExactSpelling=true)]private static extern int RoGetActivationFactory(IntPtr name,ref Guid iid,out IntPtr factory);
}
