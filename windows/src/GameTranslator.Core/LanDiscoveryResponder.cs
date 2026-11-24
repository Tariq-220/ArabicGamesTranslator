// SPDX-License-Identifier: AGPL-3.0-only
using System.Net;
using System.Net.Sockets;
using System.Text;
namespace GameTranslator.Core;

/// <summary>Small nonce-bound LAN discovery. The receiving TV uses the datagram's IP,
/// never a hostname supplied by an untrusted broadcast responder.</summary>
internal sealed class LanDiscoveryResponder : IDisposable
{
    private readonly UdpClient socket;
    private readonly CancellationTokenSource lifetime=new();
    private readonly int httpPort;
    private LanDiscoveryResponder(UdpClient socket,int port){this.socket=socket;httpPort=port;_ = Task.Run(Receive);}
    public static LanDiscoveryResponder? TryStart(int httpPort)
    {
        try{var udp=new UdpClient(AddressFamily.InterNetwork);udp.Client.ExclusiveAddressUse=false;udp.Client.SetSocketOption(SocketOptionLevel.Socket,SocketOptionName.ReuseAddress,true);udp.Client.Bind(new IPEndPoint(IPAddress.Any,8788));return new(udp,httpPort);}catch(SocketException){return null;}
    }
    internal static string? Reply(string message,IPAddress address,int port)
    {
        var bytes=address.GetAddressBytes();if(bytes.Length!=4||!(bytes[0]==10||bytes[0]==192&&bytes[1]==168||bytes[0]==172&&bytes[1] is >=16 and <=31))return null;
        const string prefix="ARABICGAMES_DISCOVER_V1 ";if(!message.StartsWith(prefix,StringComparison.Ordinal))return null;
        var nonce=message[prefix.Length..];if(nonce.Length!=32||!nonce.All(c=>char.IsAsciiHexDigit(c)))return null;
        return $"ARABICGAMES_V1 {nonce} {port}";
    }
    private async Task Receive()
    {
        try{while(!lifetime.IsCancellationRequested){var packet=await socket.ReceiveAsync(lifetime.Token).ConfigureAwait(false);if(packet.Buffer.Length>128)continue;var reply=Reply(Encoding.ASCII.GetString(packet.Buffer),packet.RemoteEndPoint.Address,httpPort);if(reply is not null)await socket.SendAsync(Encoding.ASCII.GetBytes(reply),packet.RemoteEndPoint,lifetime.Token).ConfigureAwait(false);}}
        catch(OperationCanceledException){}catch(ObjectDisposedException){}catch(SocketException){}
    }
    public void Dispose(){lifetime.Cancel();socket.Dispose();}
}
