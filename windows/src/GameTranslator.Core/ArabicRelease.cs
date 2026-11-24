// SPDX-License-Identifier: AGPL-3.0-only
using System.Text.Json;
namespace GameTranslator.Core;
public static class ArabicRelease {
 private static readonly JsonDocument Document=JsonDocument.Parse(typeof(ArabicRelease).Assembly.GetManifestResourceStream("arabic-release.json")!);
 public static string Version=>Document.RootElement.GetProperty("version").GetString()!;
 public static string TvVersion=>Document.RootElement.TryGetProperty("tvVersion",out var value)?value.GetString()??"":"";
 public static string Repository=>Document.RootElement.GetProperty("repository").GetString()??"";
}
