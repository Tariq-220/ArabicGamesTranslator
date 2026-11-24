// SPDX-License-Identifier: AGPL-3.0-only
namespace GameTranslator.Core;

public enum ReadingAreaKind { Dialogue, Phone, Menu, Instructions }

/// <summary>Source coordinates and output placement are independent; all coordinates are normalized.</summary>
public sealed record ReadingArea(string Id, string Name, ReadingAreaKind Kind, CropRegion Source,
    CropRegion Output, bool Enabled = true, int FontSize = 24, int Background = 0,
    int DurationMs = 3500, int IntervalMs = 450)
{
    public ReadingArea Validate() => this with {
        Id = string.IsNullOrWhiteSpace(Id) ? Guid.NewGuid().ToString("N") : Id,
        Name = string.IsNullOrWhiteSpace(Name) ? "منطقة قراءة" : Name[..Math.Min(60,Name.Length)],
        Source = Bound(Source), Output = Bound(Output), FontSize = Math.Clamp(FontSize,14,48),
        Background = Math.Clamp(Background,0,100), DurationMs = Math.Clamp(DurationMs,1200,30000),
        IntervalMs = Math.Clamp(IntervalMs,250,2000)
    };
    public static CropRegion Bound(CropRegion r)
    {
        double N(double n,double fallback) => double.IsFinite(n)?Math.Clamp(n,0,1):fallback;
        var left=N(r.Left,.1);var top=N(r.Top,.7);
        left=Math.Min(left,.97);top=Math.Min(top,.97);
        return new(top,Math.Clamp(N(r.Bottom,.97),top+.03,1),left,Math.Clamp(N(r.Right,.9),left+.03,1));
    }
    public static ReadingArea Phone() => new(Guid.NewGuid().ToString("N"),"رسائل الجوال",ReadingAreaKind.Phone,
        new(.15,.85,.58,.94),new(.15,.85,.20,.56),DurationMs:8000);
    public static ReadingArea Instructions() => new(Guid.NewGuid().ToString("N"),"هدف المهمة",ReadingAreaKind.Instructions,
        new(.935,.995,.10,.90),new(.84,.92,.10,.90),DurationMs:5000);
    public static ReadingArea Menu() => new(Guid.NewGuid().ToString("N"),"القائمة",ReadingAreaKind.Menu,
        new(.14,.86,.08,.47),new(.14,.86,.50,.90),DurationMs:6000);
}

public sealed record AreaCaption(string Id, long Revision, string Text, CropRegion Output,
    int FontSize, int Background, int DurationMs, bool Clear = false);
