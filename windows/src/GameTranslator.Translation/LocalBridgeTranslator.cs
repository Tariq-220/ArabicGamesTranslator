// SPDX-License-Identifier: AGPL-3.0-only
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using GameTranslator.Core;
namespace GameTranslator.Translation;
public sealed class LocalBridgeTranslator(string model,string token,HttpClient? http=null):ITextTranslator
{
 // One CPU item at a time lets dialogue preempt phone work between messages.
 public int PreferredBatchSize=>1;
 private static readonly HttpClient Client=new(){Timeout=TimeSpan.FromSeconds(12)},HyClient=new(){Timeout=TimeSpan.FromSeconds(20)};
 public async Task<string> TranslateAsync(string text,CancellationToken cancellationToken=default)=>(await TranslateManyAsync([text],cancellationToken).ConfigureAwait(false))[0];
 public async Task<IReadOnlyList<string>> TranslateManyAsync(IReadOnlyList<string> texts,CancellationToken cancellationToken=default){
  if(texts.Count is <1 or >24)throw new ArgumentException("Invalid local batch size.");
  var names=new List<string?>();var bodies=new List<string>();
  foreach(var text in texts){var(label,body)=CaptionAppearance.SplitSpeaker(text);var name=label.Length>0?(ArabicGameNames.Speaker(label.TrimEnd(':','：'))??label.TrimEnd(':','：')):null;names.Add(name);bodies.Add(name is null?text:body);}
  if(bodies.Any(string.IsNullOrWhiteSpace))throw new TranslatorException(TranslatorStatus.OutputRejected);
  using var request=new HttpRequestMessage(HttpMethod.Post,"http://127.0.0.1:8790/translate");request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);
  request.Content=new StringContent(JsonSerializer.Serialize(new{text=bodies,source_lang="EN",target_lang="AR",model}),Encoding.UTF8,"application/json");
  HttpResponseMessage response;try{response=await(http??(model=="hy-mt"?HyClient:Client)).SendAsync(request,cancellationToken).ConfigureAwait(false);}catch(OperationCanceledException e)when(!cancellationToken.IsCancellationRequested){throw new TranslatorException(TranslatorStatus.ServiceUnreachable,e);}catch(HttpRequestException e){throw new TranslatorException(TranslatorStatus.ServiceUnreachable,e);}
  using var ownedResponse=response;
  if(response.StatusCode==System.Net.HttpStatusCode.BadRequest&&texts.Count>1&&(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false)).Contains("\"request\"",StringComparison.Ordinal)){
   var legacy=new List<string>();foreach(var text in texts)legacy.Add(await TranslateAsync(text,cancellationToken).ConfigureAwait(false));return legacy;
  }
  if(!response.IsSuccessStatusCode){var error=await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);throw new TranslatorException(response.StatusCode switch{System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden=>TranslatorStatus.KeyRejected,System.Net.HttpStatusCode.ServiceUnavailable=>TranslatorStatus.ServiceBusy,System.Net.HttpStatusCode.BadRequest when error.Contains("model_mismatch",StringComparison.Ordinal)=>TranslatorStatus.ModelRejected,_=>TranslatorStatus.EngineFailed});}
  using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));var rows=json.RootElement.GetProperty("translations");
  if(rows.GetArrayLength()!=texts.Count)throw new TranslatorException(TranslatorStatus.OutputRejected);
  var result=new List<string>();for(int i=0;i<texts.Count;i++){var translated=rows[i].GetProperty("text").GetString();if(string.IsNullOrWhiteSpace(translated)){result.Add("");continue;}result.Add(names[i] is {} name?name+": "+translated:translated);}return result;
 }
}
