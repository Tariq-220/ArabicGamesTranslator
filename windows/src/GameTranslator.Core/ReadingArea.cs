// SPDX-License-Identifier: AGPL-3.0-only
namespace GameTranslator.Core;

public enum ReadingAreaKind { Dialogue, Phone, Menu, Instructions, Notification, MapCard }

/// <summary>Source coordinates and output placement are independent; all coordinates are normalized.</summary>
public sealed record ReadingArea(string Id, string Name, ReadingAreaKind Kind, CropRegion Source,
    CropRegion Output, bool Enabled = true, int FontSize = 24, int Background = 0,
    int DurationMs = 3500, int IntervalMs = 450, CaptionAppearance? Appearance=null, bool Structured=true, bool AutoDetect=true)
{
    public static IReadOnlyList<ReadingArea> Unique(IEnumerable<ReadingArea> source) => source.Select(a=>a.Validate()).DistinctBy(a=>(a.Kind,a.Source,a.Output)).ToArray();
    public ReadingArea Validate() => this with {
        Id = string.IsNullOrWhiteSpace(Id) ? Guid.NewGuid().ToString("N") : Id,
        Name = string.IsNullOrWhiteSpace(Name) ? "منطقة قراءة" : Name[..Math.Min(60,Name.Length)],
        Source = Bound(Source??new()), Output = Bound(Output??new()), FontSize = Math.Clamp(FontSize,14,48),
        Background = Math.Clamp(Background,0,100), DurationMs = Math.Clamp(DurationMs,1200,30000),
        IntervalMs = Math.Clamp(IntervalMs,16,2000), Appearance=(Appearance??DefaultAppearance(Kind)).Validate()
    };
    public static CaptionAppearance DefaultAppearance(ReadingAreaKind kind)=>new(Align:kind==ReadingAreaKind.Dialogue?"center":"right",Vertical:kind==ReadingAreaKind.Dialogue?"bottom":"top");
    public CropRegion PresentationOutput()
    {
        var position=Appearance?.Position??"custom";if(position=="custom")return Output;if(position=="cover")return Source;
        var width=Output.Right-Output.Left;var height=Output.Bottom-Output.Top;
        var left=Math.Clamp((Source.Left+Source.Right-width)/2,0,1-width);
        var top=position=="above"?Source.Top-height-.006:Source.Bottom+.006;top=Math.Clamp(top,0,1-height);
        return Bound(new(top,top+height,left,left+width));
    }
    public static CropRegion Bound(CropRegion r)
    {
        double N(double n,double fallback) => double.IsFinite(n)?Math.Clamp(n,0,1):fallback;
        var left=N(r.Left,.1);var top=N(r.Top,.7);
        left=Math.Min(left,.97);top=Math.Min(top,.97);
        return new(top,Math.Clamp(N(r.Bottom,.97),top+.03,1),left,Math.Clamp(N(r.Right,.9),left+.03,1));
    }
    public static ReadingArea Phone() => new(Guid.NewGuid().ToString("N"),"رسائل الجوال",ReadingAreaKind.Phone,
        new(.08,.98,.03,.97),new(.12,.95,.08,.39),DurationMs:8000);
    public static ReadingArea Instructions() => new(Guid.NewGuid().ToString("N"),"هدف المهمة",ReadingAreaKind.Instructions,
        new(.935,.995,.20,.80),new(.765,.815,.10,.90),DurationMs:5000,Appearance:new(Font:"tajawal",Backdrop:"none",Align:"center",Accent:true));
    public static ReadingArea Notification()=>new(Guid.NewGuid().ToString("N"),"تنبيه / رسالة واردة",ReadingAreaKind.Notification,new(.65,.99,.68,.99),new(.40,.73,.68,.99),DurationMs:6500);
    public static ReadingArea MapCard()=>new(Guid.NewGuid().ToString("N"),"وصف المهمة بالخريطة",ReadingAreaKind.MapCard,new(.12,.36,.10,.49),new(.37,.64,.10,.49),Background:85,DurationMs:8000,AutoDetect:false,Appearance:new(Backdrop:"game",Align:"right",Vertical:"top"));
    public static ReadingArea Restore(ReadingArea current){var preset=current.Kind switch{ReadingAreaKind.Phone=>Phone(),ReadingAreaKind.Notification=>Notification(),ReadingAreaKind.Instructions=>Instructions(),ReadingAreaKind.MapCard=>MapCard(),ReadingAreaKind.Menu=>Menu(),_=>new ReadingArea("dialogue","الحوار",ReadingAreaKind.Dialogue,new(.80,.918,.10,.90),new(.585,.785,.10,.90),FontSize:32,Background:72,Appearance:new(Position:"custom"))};return current with{Source=preset.Source,Output=preset.Output,Appearance=(current.Appearance??preset.Appearance??new()) with{Position=preset.Appearance?.Position??"custom"}};}
    public static ReadingArea Menu() => new(Guid.NewGuid().ToString("N"),"القائمة",ReadingAreaKind.Menu,
        new(.01,.99,.01,.99),new(.01,.99,.01,.99),Background:100,DurationMs:6000,Appearance:new(Backdrop:"source",Align:"right",Vertical:"top",Position:"cover"));
}

public sealed record AreaCaption(string Id, long Revision, string Text, CropRegion Output,
    int FontSize, int Background, int DurationMs, bool Clear = false, CaptionAppearance? Appearance=null,
    PanelLayout? Panel=null);
