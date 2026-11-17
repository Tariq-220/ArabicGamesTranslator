// SPDX-License-Identifier: AGPL-3.0-only
using System.Net;
namespace GameTranslator.Core;

/// <summary>Keep the unauthenticated TV compatibility endpoint on a trusted local network.</summary>
internal static class LocalRequestPolicy
{
    internal static bool IsLocal(IPAddress address)
    {
        if(address.IsIPv4MappedToIPv6)address=address.MapToIPv4();
        if(IPAddress.IsLoopback(address)||address.IsIPv6LinkLocal)return true;
        var b=address.GetAddressBytes();
        if(b.Length==16)return (b[0]&0xfe)==0xfc;
        return b.Length==4&&(b[0]==10||b[0]==192&&b[1]==168||b[0]==172&&b[1]>=16&&b[1]<=31||b[0]==169&&b[1]==254);
    }
    private static bool LocalHost(string value) => IPAddress.TryParse(value,out var ip)?IsLocal(ip)
        :value.Equals("localhost",StringComparison.OrdinalIgnoreCase)||value.Equals(Environment.MachineName,StringComparison.OrdinalIgnoreCase)||value.Equals(Environment.MachineName+".local",StringComparison.OrdinalIgnoreCase);
    internal static bool Allows(IPAddress peer,string headers)
    {
        if(!IsLocal(peer))return false;
        string? host=null,origin=null;
        foreach(var line in headers.Split("\r\n").Skip(1)){
            var colon=line.IndexOf(':');if(colon<0)continue;
            var name=line[..colon].Trim();var value=line[(colon+1)..].Trim();
            if(name.Equals("Host",StringComparison.OrdinalIgnoreCase)){if(host!=null)return false;host=value;}
            if(name.Equals("Origin",StringComparison.OrdinalIgnoreCase)){if(origin!=null)return false;origin=value;}
        }
        if(host==null||!Uri.TryCreate("http://"+host,UriKind.Absolute,out var target)||target.UserInfo.Length>0||!LocalHost(target.Host))return false;
        // Packaged TV applications may use an opaque file origin. LAN viewers remain trusted.
        if(origin==null||origin=="null"||origin=="file://")return true;
        return Uri.TryCreate(origin,UriKind.Absolute,out var source)&&source.Scheme is "http" or "https"&&source.UserInfo.Length==0&&LocalHost(source.Host);
    }
}
