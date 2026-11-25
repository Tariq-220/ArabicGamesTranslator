// SPDX-License-Identifier: AGPL-3.0-only
using System.Text;
using System.Text.Json;
namespace GameTranslator.Translation;

/// <summary>Incremental, bounded local reader. An unfinished writer line is retried on the next poll.</summary>
public sealed class ComparisonLogTail(string path)
{
    private long position;
    public IReadOnlyList<ComparisonEntry> ReadNew()
    {
        if(!File.Exists(path))return [];
        using var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
        if(file.Length<position)position=0;
        bool skipFirst=false;if(position==0&&file.Length>512_000){position=file.Length-512_000;skipFirst=true;}
        file.Position=position;var bytes=new byte[(int)Math.Min(512_000,file.Length-position)];int count=file.Read(bytes);
        int end=Array.LastIndexOf(bytes,(byte)'\n',count-1,count);if(end<0)return [];
        position+=end+1;var lines=Encoding.UTF8.GetString(bytes,0,end+1).Split('\n');var entries=new List<ComparisonEntry>();
        foreach(var line in lines.Skip(skipFirst?1:0)){
            if(line.Length is 0 or >32000)continue;
            try{if(JsonSerializer.Deserialize<ComparisonEntry>(line) is {} entry)entries.Add(entry);}catch(JsonException){}
        }
        return entries.TakeLast(400).ToArray();
    }
}
