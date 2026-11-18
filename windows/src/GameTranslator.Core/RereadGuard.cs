// SPDX-License-Identifier: AGPL-3.0-only
namespace GameTranslator.Core;
/// <summary>Keeps a recent complete reading when OCR loses words or joins them during fading. No dictionary guessing.</summary>
public sealed class RereadGuard {
 private string last="";private long seen;
 public string Filter(string text,long now){
  if(string.IsNullOrWhiteSpace(text)){if(now-seen>800)Reset();return text;}
  if(text==last){seen=now;return text;}
  if(last.Length>0&&now-seen<=1500&&text.Length<=400&&last.Length<=400){
   var before=Words(last);var after=Words(text);
   // Losing an unfinished tail shortly after a full reading is a common fade-out artifact.
   if(now-seen<=650&&before.Length>=3&&after.Length>=2&&after.Length<before.Length&&!EndsSentence(text)&&IsPrefix(after,before))return last;
   // E.g. "a nice fresh day for flying" -> "anicefreshdayfornying".
   if(before.Length>=5&&after.Length>0&&after.Length<=before.Length-2&&after.Max(x=>x.Length)>=19&&after.Max(x=>x.Length)>=before.Max(x=>x.Length)+8){
    var a=Compact(last);var b=Compact(text);if(a.Length>=20&&Near(a,b))return last;
   }
  }
  last=text;seen=now;return text;
 }
 public void Reset(){last="";seen=0;}
 private static string[] Words(string s)=>s.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries).Select(t=>t.Trim('.',',','!','?','…','\"','\'',':',';').ToLowerInvariant()).Where(t=>t.Length>0).ToArray();
 private static bool EndsSentence(string s)=>s.TrimEnd().TrimEnd('\"','\'','”','’') is {Length:>0} end&&".!?…".Contains(end[^1]);
 private static bool IsPrefix(string[] small,string[] big)=>small.Select((word,i)=>word==big[i]).All(x=>x);
 private static string Compact(string s)=>new(s.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
 private static bool Near(string a,string b){
  var limit=Math.Max(a.Length,b.Length)/10;if(Math.Abs(a.Length-b.Length)>limit)return false;
  var row=Enumerable.Range(0,b.Length+1).ToArray();for(int i=1;i<=a.Length;i++){var prev=row[0];row[0]=i;for(int j=1;j<=b.Length;j++){var old=row[j];row[j]=Math.Min(Math.Min(row[j]+1,row[j-1]+1),prev+(a[i-1]==b[j-1]?0:1));prev=old;}}
  return row[b.Length]<=limit;
 }
}
