using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace GameTranslator.Translation.Tests;
public class GitHubUpdateTests {
 private const string Repo="example-owner/ArabicGamesTranslator";
 private const string Asset="ArabicGames-Windows-0.4.9-Full-Install.zip";
 private static Uri Url=>new($"https://github.com/{Repo}/releases/download/v0.4.9/{Asset}");
 private sealed class Handler(Func<HttpRequestMessage,HttpResponseMessage> action):HttpMessageHandler {protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)=>Task.FromResult(action(request));}
 private static byte[] Archive(string extra="safe.txt") {using var mem=new MemoryStream();using(var zip=new ZipArchive(mem,ZipArchiveMode.Create,true))foreach(var name in new[]{"Install.ps1","manifest.json","ArabicGames-Windows/GameTranslator.exe",extra}){using var writer=new StreamWriter(zip.CreateEntry(name).Open());writer.Write("test");}return mem.ToArray();}
 private static string Metadata(string url,string digest)=>JsonSerializer.Serialize(new{tag_name="v0.4.9",draft=false,prerelease=false,assets=new[]{new{name=Asset,size=123,digest,browser_download_url=url}}});
 [Fact]public async Task OnlyConfiguredGitHubRepositoryMaySupplyUpdate(){
  using var http=new HttpClient(new Handler(_=>new(HttpStatusCode.OK){Content=new StringContent(Metadata("https://evil.example/update.zip","sha256:"+new string('a',64)))}));
  await Assert.ThrowsAsync<InvalidDataException>(()=>new GitHubUpdateClient(Repo,http).CheckAsync(new Version("0.4.8")));
 }
 [Fact]public async Task MissingDigestPreventsUpdate(){using var http=new HttpClient(new Handler(_=>new(HttpStatusCode.OK){Content=new StringContent(Metadata(Url.AbsoluteUri,""))}));await Assert.ThrowsAsync<InvalidDataException>(()=>new GitHubUpdateClient(Repo,http).CheckAsync(new Version("0.4.8")));}
 [Fact]public async Task DownloadChecksActualBytesBeforeExtraction(){
  var bytes=Archive();using var http=new HttpClient(new Handler(_=>new(HttpStatusCode.OK){Content=new ByteArrayContent(bytes)}));
  var folder=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));var target=Path.Combine(folder,"update.zip");
  try{var release=new ReleaseDownload(new("0.4.9"),Url,bytes.Length,Convert.ToHexString(SHA256.HashData(bytes)));await new GitHubUpdateClient(Repo,http).DownloadAsync(release,target);GitHubUpdateClient.Extract(target,Path.Combine(folder,"unpacked"));Assert.True(File.Exists(Path.Combine(folder,"unpacked","Install.ps1")));}finally{if(Directory.Exists(folder))Directory.Delete(folder,true);}
 }
 [Fact]public async Task CorruptDownloadLeavesNoRunnablePackage(){var bytes=Encoding.UTF8.GetBytes("corrupt");using var http=new HttpClient(new Handler(_=>new(HttpStatusCode.OK){Content=new ByteArrayContent(bytes)}));var folder=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));var target=Path.Combine(folder,"update.zip");try{await Assert.ThrowsAsync<InvalidDataException>(()=>new GitHubUpdateClient(Repo,http).DownloadAsync(new(new("0.4.9"),Url,bytes.Length,new string('0',64)),target));Assert.False(File.Exists(target));Assert.False(File.Exists(target+".part"));}finally{if(Directory.Exists(folder))Directory.Delete(folder,true);}}
 [Fact]public async Task RedirectToAnotherHostIsRejected(){using var http=new HttpClient(new Handler(_=>{var r=new HttpResponseMessage(HttpStatusCode.Redirect);r.Headers.Location=new Uri("https://evil.example/download");return r;}));var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"),"update.zip");try{await Assert.ThrowsAsync<InvalidDataException>(()=>new GitHubUpdateClient(Repo,http).DownloadAsync(new(new("0.4.9"),Url,1,new string('0',64)),path));}finally{Directory.Delete(Path.GetDirectoryName(path)!,true);}}
 [Theory][InlineData("../escape.txt")][InlineData("..\\escape.txt")][InlineData("C:/escape.txt")]
 public void ArchiveCannotWriteOutsideUpdateFolder(string bad){var folder=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);try{var zip=Path.Combine(folder,"update.zip");File.WriteAllBytes(zip,Archive(bad));Assert.Throws<InvalidDataException>(()=>GitHubUpdateClient.Extract(zip,Path.Combine(folder,"unpack")));Assert.False(File.Exists(Path.Combine(folder,"escape.txt")));}finally{Directory.Delete(folder,true);}}
}
