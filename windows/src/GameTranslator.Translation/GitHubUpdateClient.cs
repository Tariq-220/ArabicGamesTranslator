// SPDX-License-Identifier: AGPL-3.0-only
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace GameTranslator.Translation;
public sealed record ReleaseDownload(Version Version,Uri Url,long Size,string Sha256);
/// <summary>HTTPS GitHub release updates. Trust is restricted to the configured repository.</summary>
public sealed class GitHubUpdateClient {
 private readonly HttpClient http;
 private readonly string repository;
 private static readonly HttpClient Shared=new(new HttpClientHandler {AllowAutoRedirect=false}) {Timeout=TimeSpan.FromMinutes(20)};
 public GitHubUpdateClient(string repository,HttpClient? client=null){
  if(!Regex.IsMatch(repository,@"^[A-Za-z0-9][A-Za-z0-9-]{0,38}/[A-Za-z0-9_.-]{1,100}$")||repository.Split('/')[1] is "." or "..")throw new ArgumentException("Invalid GitHub repository.");
  this.repository=repository;http=client??Shared;
 }
 private static HttpRequestMessage Request(Uri url){var request=new HttpRequestMessage(HttpMethod.Get,url);request.Headers.UserAgent.ParseAdd("ArabicGames-Updater/1.0");return request;}
 public async Task<ReleaseDownload?> CheckAsync(Version current,CancellationToken cancellation=default){
  using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellation);timeout.CancelAfter(TimeSpan.FromSeconds(15));
  using var request=Request(new Uri($"https://api.github.com/repos/{repository}/releases/latest"));using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,timeout.Token);
  if(response.StatusCode==HttpStatusCode.NotFound)return null;
  response.EnsureSuccessStatusCode();if(response.Content.Headers.ContentLength>1024*1024)throw new InvalidDataException("Release metadata too large.");
  using var stream=await response.Content.ReadAsStreamAsync(timeout.Token);using var memory=new MemoryStream();var bytes=new byte[8192];int n;
  while((n=await stream.ReadAsync(bytes,timeout.Token))>0){if(memory.Length+n>1024*1024)throw new InvalidDataException("Release metadata too large.");memory.Write(bytes,0,n);}
  using var json=JsonDocument.Parse(memory.ToArray());var root=json.RootElement;
  if(root.TryGetProperty("draft",out var draft)&&draft.GetBoolean()||root.TryGetProperty("prerelease",out var pre)&&pre.GetBoolean())return null;
  var tag=root.GetProperty("tag_name").GetString()??"";
  if(!Regex.IsMatch(tag,@"^v\d+\.\d+\.\d+$")||!Version.TryParse(tag[1..],out var version)||version<=current)return null;
  var filenames=new[]{$"ArabicGames-Windows-{version}-Update.zip",$"ArabicGames-Windows-{version}-Full-Install.zip"};
  foreach(var filename in filenames)foreach(var asset in root.GetProperty("assets").EnumerateArray()){
   if(asset.GetProperty("name").GetString()!=filename)continue;
   var size=asset.GetProperty("size").GetInt64();var digest=asset.GetProperty("digest").GetString()??"";
   if(size<1||size>1024L*1024*1024||!Regex.IsMatch(digest,@"^sha256:[0-9a-fA-F]{64}$"))throw new InvalidDataException("Missing trusted asset checksum or invalid size.");
   var url=new Uri(asset.GetProperty("browser_download_url").GetString()!);
   var expected=$"https://github.com/{repository}/releases/download/{tag}/{filename}";
   if(url.AbsoluteUri!=expected)throw new InvalidDataException("Unexpected update download origin.");
   return new(version,url,size,digest[7..].ToLowerInvariant());
  }
  return null;
 }
 private static bool Allowed(Uri url)=>url.Scheme=="https"&&url.IsDefaultPort&&string.IsNullOrEmpty(url.UserInfo)&&url.Host is "github.com" or "release-assets.githubusercontent.com" or "objects.githubusercontent.com";
 public async Task DownloadAsync(ReleaseDownload release,string target,IProgress<double>? progress=null,CancellationToken cancellation=default){
  if(!Allowed(release.Url)||release.Url.Host!="github.com"||!release.Url.AbsolutePath.StartsWith('/'+repository+"/releases/download/",StringComparison.Ordinal))throw new InvalidDataException("Unexpected update origin.");
  Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(target))!);var partial=target+".part";
  try{
   var url=release.Url;HttpResponseMessage? response=null;
   for(int redirects=0;redirects<5;redirects++){
    using var request=Request(url);response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,cancellation);
    if((int)response.StatusCode is >=300 and <=399){var next=response.Headers.Location;response.Dispose();response=null;if(next is null)throw new InvalidDataException("Missing redirect.");url=next.IsAbsoluteUri?next:new Uri(url,next);if(!Allowed(url))throw new InvalidDataException("Unexpected update redirect.");continue;}
    break;
   }
   using(response){if(response is null)throw new InvalidDataException("Too many update redirects.");response.EnsureSuccessStatusCode();
    await using var input=await response.Content.ReadAsStreamAsync(cancellation);await using(var output=new FileStream(partial,FileMode.CreateNew,FileAccess.Write,FileShare.None,65536,true)){
     using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);var buffer=new byte[65536];long count=0;int read;
     while((read=await input.ReadAsync(buffer,cancellation))>0){count+=read;if(count>release.Size)throw new InvalidDataException("Update size exceeded.");hash.AppendData(buffer,0,read);await output.WriteAsync(buffer.AsMemory(0,read),cancellation);progress?.Report((double)count/release.Size);}
     if(count!=release.Size||!Convert.ToHexString(hash.GetHashAndReset()).Equals(release.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Update checksum mismatch.");
    }
   }
   File.Move(partial,target,overwrite:false);
  }catch{if(File.Exists(partial))File.Delete(partial);throw;}
 }
 public static void Extract(string archive,string target){
  if(Directory.Exists(target))throw new IOException("Update staging folder already exists.");
  using var zip=ZipFile.OpenRead(archive);if(zip.Entries.Count>10000)throw new InvalidDataException("Too many update entries.");long total=0;
  foreach(var entry in zip.Entries){
   var path=entry.FullName.Replace('\\','/');if(path.StartsWith('/')||path.Contains(':')||path.Split('/').Any(p=>p=="..")||((entry.ExternalAttributes>>16)&0xF000)==0xA000)throw new InvalidDataException("Unsafe archive path.");
   total+=entry.Length;if(total>2L*1024*1024*1024)throw new InvalidDataException("Unpacked update too large.");
  }
  bool full=zip.GetEntry("Install.ps1") is not null&&zip.GetEntry("manifest.json") is not null&&zip.GetEntry("ArabicGames-Windows/GameTranslator.exe") is not null;
  bool repair=zip.GetEntry("Reading-Fix.ps1") is not null&&zip.GetEntry("patch.json") is not null&&zip.GetEntry("files/GameTranslator.exe") is not null;
  if(!full&&!repair)throw new InvalidDataException("Incomplete update package.");
  Directory.CreateDirectory(target);zip.ExtractToDirectory(target);
 }
}
