namespace GameTranslator.Core;
/// <summary>Timing controls are independent from fade and caption lifetime.</summary>
public sealed record ReadingTiming(int CaptureFps=12,int PreviewFps=10,int? AreaReadMs=null,int SettleMs=120,int GoneMs=800,bool FullResolutionPreview=false,int MinimumDisplayMs=700)
{
 public ReadingTiming Validate()=>this with{CaptureFps=Math.Clamp(CaptureFps,1,60),PreviewFps=Math.Clamp(PreviewFps,1,60),AreaReadMs=AreaReadMs is {} n?Math.Clamp(n,16,2000):null,SettleMs=Math.Clamp(SettleMs,0,1000),GoneMs=Math.Clamp(GoneMs,50,5000),MinimumDisplayMs=Math.Clamp(MinimumDisplayMs,0,3000)};
}
