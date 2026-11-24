// SPDX-License-Identifier: AGPL-3.0-only
using GameTranslator.Core;
namespace GameTranslator.Core.Tests;
public class XboxNavigationTests
{
    [Theory]
    [InlineData("https://play.xbox.com/remoteplay",true)]
    [InlineData("https://login.live.com/oauth20_authorize.srf",true)]
    [InlineData("https://xbox.com.evil.example/",false)]
    [InlineData("https://evilxbox.com/",false)]
    [InlineData("http://login.live.com/",false)]
    [InlineData("file:///etc/passwd",false)]
    public void NavigationStaysInsideExactMicrosoftDomainBoundaries(string url,bool allowed)=>Assert.Equal(allowed,XboxNavigation.Allowed(url));
}
