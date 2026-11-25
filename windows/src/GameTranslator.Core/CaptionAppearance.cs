// SPDX-License-Identifier: AGPL-3.0-only
using System.Text.Json;
using System.Text.RegularExpressions;
namespace GameTranslator.Core;

/// <summary>Portable rendering settings. Same identifiers are understood by desktop and TV.</summary>
public sealed record CaptionAppearance(string Font="noto", string Backdrop="game", string Align="center",
    string Vertical="bottom", string Position="custom", bool MatchSize=true, bool SpeakerColors=true,
    string SpeakerColor="auto", bool Outline=true, int FadeMs=100, bool Accent=false)
{
    public CaptionAppearance Validate()=>this with {
        Font=Fonts.Contains(Font)?Font:"noto", Backdrop=Backdrops.Contains(Backdrop)?Backdrop:"game",
        Align=Align is "left" or "right" or "center"?Align:"center",
        Vertical=Vertical is "top" or "center" or "bottom"?Vertical:"bottom",
        Position=Positions.Contains(Position)?Position:"custom", SpeakerColor=SpeakerColor=="auto"||IsColor(SpeakerColor)?SpeakerColor:"auto",
        FadeMs=Math.Clamp(FadeMs,0,300) };
    public static readonly string[] Fonts=["noto","tajawal","cairo","changa","lalezar"];
    public static readonly string[] Backdrops=["none","game","rounded","rectangle"];
    public static readonly string[] Positions=["custom","above","cover","below"];
    public static bool IsColor(string? color)=>color is not null&&Regex.IsMatch(color,"^#[0-9a-fA-F]{6}$",RegexOptions.CultureInvariant);
    public static string SpeakerAccent(string text,CaptionAppearance? appearance)
    {
        var a=appearance??new();if(!a.SpeakerColors)return "#FFFFFF";
        if(IsColor(a.SpeakerColor))return a.SpeakerColor;
        var name=text.Split(':','：')[0].Trim();
        return name switch { "جيسون" or "جايسون" or "Jason"=>"#F2D87A", "لوسيا" or "لوسيا كامينوس" or "Lucia"=>"#F39ABA", _=>"#70DDE7" };
    }
    public static (string Name,string Body) SplitSpeaker(string text)
    {
        var i=text.IndexOfAny([':','：']);return i>0&&i<45&&!text[..i].Contains('\n')?(text[..(i+1)],text[(i+1)..].TrimStart()):("",text);
    }
}

/// <summary>Portable region layout; no keys, account names or network addresses are exported.</summary>
public sealed record ReadingLayout(int Schema, ReadingArea[] Areas)
{
    public string Export()=>JsonSerializer.Serialize(this,new JsonSerializerOptions{WriteIndented=true});
    public static ReadingLayout Import(string json)
    {
        if(json.Length>64000)throw new ArgumentException("ملف التخطيط أكبر من الحد المسموح.");
        var layout=JsonSerializer.Deserialize<ReadingLayout>(json)??throw new ArgumentException("ملف التخطيط فارغ.");
        if(layout.Schema!=1||layout.Areas is null||layout.Areas.Length is <1 or >9||layout.Areas.Any(a=>a is null)||layout.Areas.Count(a=>a.Kind==ReadingAreaKind.Dialogue)!=1)
            throw new ArgumentException("ملف تخطيط غير متوافق؛ يجب وجود منطقة حوار واحدة وحتى 8 مناطق إضافية.");
        var ids=new HashSet<string>();return layout with{Areas=layout.Areas.Select(a=>{var v=a.Validate();if(!Enum.IsDefined(v.Kind)||!ids.Add(v.Id))throw new ArgumentException("منطقة غير صالحة أو مكررة.");return v;}).ToArray()};
    }
}
