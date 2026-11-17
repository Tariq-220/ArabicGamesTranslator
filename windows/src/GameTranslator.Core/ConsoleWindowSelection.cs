// SPDX-License-Identifier: AGPL-3.0-only
namespace GameTranslator.Core;
public static class ConsoleWindowSelection {
 public static string? Pick(IEnumerable<string> titles,bool playStation,string? detectedProcessTitle=null){
  var available=titles.ToArray();
  if(!string.IsNullOrWhiteSpace(detectedProcessTitle)&&available.Contains(detectedProcessTitle))return detectedProcessTitle;
  if(playStation)return available.FirstOrDefault(t=>t.Equals("PS Remote Play",StringComparison.OrdinalIgnoreCase)||t.Equals("PSRemotePlay",StringComparison.OrdinalIgnoreCase)||t.StartsWith("chiaki",StringComparison.OrdinalIgnoreCase));
  return available.FirstOrDefault(t=>t.Contains("Xbox",StringComparison.OrdinalIgnoreCase));
 }
}
