// SPDX-License-Identifier: AGPL-3.0-only
using System.Text.Json;
using System.Threading.Channels;
using GameTranslator.Core;
namespace GameTranslator.Translation;

public sealed record ComparisonEntry(DateTimeOffset Time, string Session, string Game, string Engine,
    string English, string Arabic, LineMetrics? Metrics, CropRegion Region, bool LatestOnly, string? Method=null, string? Edition=null, int? CaptureGapMs=null, string? Console=null,string? OcrLanguage=null,string? Error=null,int? RulesRevision=null,bool? PublicCorrections=null);

/// <summary>Local, bounded background log. No credentials, images or network access.</summary>
public sealed class ComparisonLog : IAsyncDisposable
{
    private readonly Channel<ComparisonEntry> queue = Channel.CreateBounded<ComparisonEntry>(new BoundedChannelOptions(256) { SingleReader=true, FullMode=BoundedChannelFullMode.Wait });
    private readonly Task writer;
    private readonly long maxBytes;
    public string FilePath { get; }
    public string? Error { get; private set; }
    public ComparisonLog(string path, long maxBytes=10*1024*1024) { FilePath=path;this.maxBytes=maxBytes;writer=Task.Run(WriteAsync); }
    public bool Add(ComparisonEntry entry) => queue.Writer.TryWrite(entry);
    private async Task WriteAsync()
    {
        await foreach (var entry in queue.Reader.ReadAllAsync())
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(FilePath))!);
                if (File.Exists(FilePath) && new FileInfo(FilePath).Length>=maxBytes)
                    File.Move(FilePath,FilePath+".previous",overwrite:true);
                await File.AppendAllTextAsync(FilePath,JsonSerializer.Serialize(entry)+Environment.NewLine);
                Error=null;
            }
            catch (Exception e) when(e is IOException or UnauthorizedAccessException) { Error="تعذر حفظ سجل المقارنة"; }
        }
    }
    public async ValueTask DisposeAsync() { queue.Writer.TryComplete();await writer.ConfigureAwait(false); }
}
