// SPDX-License-Identifier: AGPL-3.0-only
// Microsoft domain boundary policy follows VI src-tauri/src/xbox.rs (AGPL-3.0-only).
namespace GameTranslator.Core;
public static class XboxNavigation
{
    private static readonly string[] Hosts=["xbox.com","live.com","microsoft.com","microsoftonline.com","msftauth.net","msauth.net","live.net","xboxlive.com","akamaized.net","msecnd.net","office.com"];
    public static bool Allowed(string address) => Uri.TryCreate(address,UriKind.Absolute,out var uri)
        && uri.Scheme==Uri.UriSchemeHttps && Hosts.Any(host=>uri.IdnHost.Equals(host,StringComparison.OrdinalIgnoreCase)
            ||uri.IdnHost.EndsWith("."+host,StringComparison.OrdinalIgnoreCase));
}
