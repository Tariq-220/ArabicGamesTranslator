// SPDX-License-Identifier: AGPL-3.0-only
// Public GameTranslator pack protocol and canonical keys by Sefflex / Sinan Yorulmaz (AGPL-3.0).
using System.IO.Compression;
using System.Collections.Frozen;
using System.Security.Cryptography;
using System.Text.Json;
using GameTranslator.Core;
namespace GameTranslator.Translation;

/// <summary>Language-isolated, exact-key GTA VI memory. Data downloads directly from the public upstream;
/// nothing is posted to it. Failed updates leave the verified local pack available.</summary>
public sealed class ArabicGamePackStore(string directory,HttpClient? client=null)
{
    public static ArabicGamePackStore Shared {get;}=new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ArabicGames","ArabicPacks"));
    private static readonly HttpClient Http=new(){Timeout=TimeSpan.FromSeconds(60)};
    private const string Host="https://gametranslator-packs.gametranslator-pool.workers.dev/";
    private readonly SemaphoreSlim gate=new(1);
    private GamePack? loaded;
    public bool Enabled {get;set;}=true;
    public string Status {get;private set;}="لم تُحمّل الحزمة العربية بعد";
    public int Entries=>Volatile.Read(ref loaded)?.Entries.Count??0;
    public event Action? Changed;
    public static bool Applies(string game)=>game.Replace(" ","").Equals("GTA6",StringComparison.OrdinalIgnoreCase)||game.Contains("Grand Theft Auto VI",StringComparison.OrdinalIgnoreCase);
    public string? Find(string game,string source)
    {
        var pack=Volatile.Read(ref loaded);if(!Enabled||pack is null||!Applies(game))return null;
        string? Exact(string text){var key=PackKey.Key(text);return key is not null&&PackKey.Number(key) is {} number?(pack.Entries.GetValueOrDefault(number)??pack.Segments.GetValueOrDefault(number)):null;}
        string? Piece(string text){var full=Exact(text);if(full is not null)return full;var(name,body)=CaptionAppearance.SplitSpeaker(text);var hit=name.Length>0?Exact(body):null;return hit is null?null:ArabicGameNames.Speaker(name.TrimEnd(':','：'))+": "+hit;}
        var result=Piece(ArabicOutputQuality.DialogueInput(source));
        if(result is null&&source.Contains('\n')){var rows=source.Split('\n',StringSplitOptions.RemoveEmptyEntries).Select(Piece).ToArray();if(rows.All(v=>v is not null))result=string.Join('\n',rows!);}
        return result is not null&&ArabicOutputQuality.Accepts(source,result)?ArabicGameNames.Localize(source,result):null;
    }
    public async Task EnsureAsync(bool force=false,CancellationToken token=default)
    {
        if(!Enabled||!await gate.WaitAsync(0,token).ConfigureAwait(false))return;
        try
        {
            Directory.CreateDirectory(directory);var path=Path.Combine(directory,"gta-6-ar.pack");var meta=path+".sha256";
            if(Entries==0&&File.Exists(path)&&File.Exists(meta)){
                var data=await File.ReadAllBytesAsync(path,token).ConfigureAwait(false);var wanted=(await File.ReadAllTextAsync(meta,token).ConfigureAwait(false)).Trim();
                if(data.Length<=32_000_000&&Convert.ToHexStringLower(SHA256.HashData(data))==wanted)await Load(data,token).ConfigureAwait(false);
            }
            var stamp=Path.Combine(directory,"checked");if(!force&&Entries>0&&File.Exists(stamp)&&DateTime.UtcNow-File.GetLastWriteTimeUtc(stamp)<TimeSpan.FromDays(7))return;
            Status="جارٍ فحص الحزمة العربية الجاهزة لـ GTA 6…";Changed?.Invoke();
            using var listing=await Get("v1/packs",token).ConfigureAwait(false);var bytes=await Limited(listing,200_000,token).ConfigureAwait(false);using var doc=JsonDocument.Parse(bytes);
            var pack=doc.RootElement.GetProperty("packs").EnumerateArray().FirstOrDefault(p=>p.GetProperty("id").GetString()=="gta-6-ar"&&p.GetProperty("lang").GetString()=="ar");
            if(pack.ValueKind==JsonValueKind.Undefined)throw new InvalidDataException("Arabic pack not listed");
            var sha=pack.GetProperty("sha256").GetString()??"";long size=pack.GetProperty("size").GetInt64();if(size is <1 or >32_000_000||sha.Length!=64||!sha.All(Uri.IsHexDigit))throw new InvalidDataException("Invalid pack metadata");
            if(Entries>0&&File.Exists(meta)&&(await File.ReadAllTextAsync(meta,token).ConfigureAwait(false)).Trim()==sha){await File.WriteAllTextAsync(stamp,"",token).ConfigureAwait(false);Status=$"الحزمة العربية جاهزة · {Entries:N0} مدخل";return;}
            Status="جارٍ تنزيل الحزمة العربية مرة واحدة (نحو 20 ميجابايت)…";Changed?.Invoke();
            using var response=await Get("v1/packs/gta-6-ar",token).ConfigureAwait(false);var dataNew=await Limited(response,32_000_000,token).ConfigureAwait(false);
            if(dataNew.Length!=size||Convert.ToHexStringLower(SHA256.HashData(dataNew))!=sha)throw new InvalidDataException("Pack checksum mismatch");
            await Load(dataNew,token).ConfigureAwait(false);
            await File.WriteAllBytesAsync(path+".tmp",dataNew,token).ConfigureAwait(false);File.Move(path+".tmp",path,true);await File.WriteAllTextAsync(meta,sha,token).ConfigureAwait(false);await File.WriteAllTextAsync(stamp,"",token).ConfigureAwait(false);
        }
        catch(OperationCanceledException)when(token.IsCancellationRequested){}
        catch(Exception e)when(e is IOException or HttpRequestException or JsonException or InvalidOperationException or TaskCanceledException or KeyNotFoundException){Status=Entries>0?$"الحزمة المحلية جاهزة · تعذر تحديثها ({e.GetType().Name})":"الحزمة غير متاحة الآن؛ سيُستخدم المحرك · "+e.GetType().Name;}
        finally{Changed?.Invoke();gate.Release();}
    }
    private async Task Load(byte[] data,CancellationToken token)
    {
        var pack=await Task.Run(()=>{
            // Bound inflated JSON as well as transport; a corrupt gzip must not exhaust memory.
            using var input=new MemoryStream(data);using var zip=new GZipStream(input,CompressionMode.Decompress);using var output=new MemoryStream();var buffer=new byte[65536];int count;while((count=zip.Read(buffer))>0){token.ThrowIfCancellationRequested();if(output.Length+count>180_000_000)throw new InvalidDataException("Inflated pack too large");output.Write(buffer,0,count);}
            using(var doc=JsonDocument.Parse(output.GetBuffer().AsMemory(0,(int)output.Length)))if(doc.RootElement.GetProperty("id").GetString()!="gta-6-ar"||doc.RootElement.GetProperty("lang").GetString()!="ar")throw new InvalidDataException("Wrong pack language");
            var decoded=GamePack.Decode(output.ToArray(),"gta-6-ar",includeIndex:false);
            return new GamePack(decoded.Id,decoded.Title,decoded.Entries.ToFrozenDictionary(),decoded.Segments.ToFrozenDictionary(),keys:[]);
        },token).ConfigureAwait(false);Volatile.Write(ref loaded,pack);Status=$"الحزمة العربية جاهزة · {pack.Entries.Count:N0} مدخل";
    }
    private async Task<HttpResponseMessage> Get(string path,CancellationToken token){using var request=new HttpRequestMessage(HttpMethod.Get,Host+path);request.Headers.UserAgent.ParseAdd("ArabicGames/0.5.5");var response=await(client??Http).SendAsync(request,HttpCompletionOption.ResponseHeadersRead,token).ConfigureAwait(false);try{response.EnsureSuccessStatusCode();return response;}catch{response.Dispose();throw;}}
    private static async Task<byte[]> Limited(HttpResponseMessage response,int limit,CancellationToken token){if(response.Content.Headers.ContentLength>limit)throw new InvalidDataException("Response too large");using var input=await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);using var output=new MemoryStream();var buffer=new byte[65536];int count;while((count=await input.ReadAsync(buffer,token).ConfigureAwait(false))>0){if(output.Length+count>limit)throw new InvalidDataException("Response too large");output.Write(buffer,0,count);}return output.ToArray();}
}
