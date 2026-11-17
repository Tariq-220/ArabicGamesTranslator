using System.Text.Json;
using GameTranslator.Core;
namespace GameTranslator.Translation.Tests;
public class ComparisonLogTests
{
    [Fact]
    public async Task SavesSeparateEngineSessionsWithMatchingTimingsAndEscapedDialogue()
    {
        var folder=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));var path=Path.Combine(folder,"comparison.jsonl");
        try {
            await using(var log=new ComparisonLog(path)) {
                Assert.True(log.Add(new(DateTimeOffset.UtcNow,"one","RDR2","DeepL","Hello\n\"Arthur\"","مرحبًا",new(20,300,380,null,LineOrigin.Engine),new(),false)));
                Assert.True(log.Add(new(DateTimeOffset.UtcNow,"two","RDR2","jisr-msa","Hello","أهلًا",new(21,100,170,null,LineOrigin.Engine),new(),true)));
            }
            var rows=File.ReadAllLines(path);Assert.Equal(2,rows.Length);
            using var first=JsonDocument.Parse(rows[0]);using var second=JsonDocument.Parse(rows[1]);
            Assert.Equal("Hello\n\"Arthur\"",first.RootElement.GetProperty("English").GetString());
            Assert.Equal(300,first.RootElement.GetProperty("Metrics").GetProperty("TranslationMs").GetInt32());
            Assert.Equal("jisr-msa",second.RootElement.GetProperty("Engine").GetString());
            Assert.Equal(100,second.RootElement.GetProperty("Metrics").GetProperty("TranslationMs").GetInt32());
        } finally {if(Directory.Exists(folder))Directory.Delete(folder,true);}
    }
    [Fact]
    public async Task RotatesAtLimitInsteadOfGrowingWithoutBound()
    {
        var folder=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));var path=Path.Combine(folder,"log");
        try {await using(var log=new ComparisonLog(path,1)) {log.Add(new(DateTimeOffset.UtcNow,"1","game","a","one","واحد",null,new(),false));log.Add(new(DateTimeOffset.UtcNow,"2","game","b","two","اثنان",null,new(),false));}
            Assert.True(File.Exists(path+".previous"));Assert.Single(File.ReadAllLines(path));
        } finally {if(Directory.Exists(folder))Directory.Delete(folder,true);}
    }
}
