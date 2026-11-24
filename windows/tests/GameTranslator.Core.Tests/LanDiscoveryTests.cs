// SPDX-License-Identifier: AGPL-3.0-only
using GameTranslator.Core;
using System.Net;
namespace GameTranslator.Core.Tests;
public class LanDiscoveryTests
{
    [Fact]public void DiscoveryBindsReplyToNonceAndActualServerPort()
    {
        const string nonce="aabbccddaabbccddaabbccddaabbccdd";
        Assert.Equal("ARABICGAMES_V1 "+nonce+" 49152",LanDiscoveryResponder.Reply("ARABICGAMES_DISCOVER_V1 "+nonce,IPAddress.Parse("10.1.2.3"),49152));
        Assert.Null(LanDiscoveryResponder.Reply("ARABICGAMES_DISCOVER_V1 "+nonce,IPAddress.Parse("8.8.8.8"),49152));
        Assert.Null(LanDiscoveryResponder.Reply("ARABICGAMES_DISCOVER_V1 injected port",IPAddress.Parse("10.1.2.3"),49152));
    }
}
