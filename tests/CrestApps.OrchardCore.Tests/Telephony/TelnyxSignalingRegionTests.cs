using CrestApps.OrchardCore.Telnyx;
using CrestApps.OrchardCore.Telnyx.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Options;
using Moq;
using CrestApps.Core.Telephony.Models;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// The Telnyx signaling region: the point of presence an agent's browser registers on.
/// </summary>
/// <remarks>
/// Telnyx picks this by looking up where the browser appears to be and documents that the answer can be wrong, so
/// a tenant can pin it instead. Everything here guards the same hazard from two sides: a region Telnyx does not
/// recognize is turned by its SDK into a signaling hostname that does not resolve, and the agent cannot register
/// at all -- a worse outcome than the geo-routing the setting was meant to correct.
/// </remarks>
public sealed class TelnyxSignalingRegionTests
{
    [Theory]
    [InlineData("eu")]
    [InlineData("us-west")]
    [InlineData("south-asia")]
    public void Normalize_KeepsARegionTelnyxKnows(string region)
    {
        Assert.Equal(region, TelnyxSignalingRegions.Normalize(region));
    }

    [Theory]
    [InlineData("  EU  ", "eu")]
    [InlineData("US-East", "us-east")]
    public void Normalize_AcceptsAHandEditedSettingInAnyCasing(string configured, string expected)
    {
        Assert.Equal(expected, TelnyxSignalingRegions.Normalize(configured));
    }

    [Theory]
    [InlineData("middle-east")]
    [InlineData("lv1-prod")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Normalize_DropsAnythingTelnyxWouldNotResolve(string configured)
    {
        // Null rather than the original value: the phone falls back to Telnyx's own routing, which works, instead
        // of to a hostname that does not exist.
        Assert.Null(TelnyxSignalingRegions.Normalize(configured));
    }

    [Fact]
    public void All_HasNoMiddleEastEntry()
    {
        // Telnyx has no edge there. Offering one would point an agent at a host that does not resolve; Europe is
        // the nearest and the list should not imply otherwise.
        Assert.DoesNotContain(TelnyxSignalingRegions.All, region => region.Contains("middle", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task BuildAsync_AdvertisesTheConfiguredRegionToTheBrowser()
    {
        // Arrange
        var contributor = CreateContributor("eu");

        // Act
        var config = await contributor.BuildAsync(new SoftPhoneRegistrationConfigContext
        {
            UserId = "user-1",
            DisplayName = "Agent One",
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("eu", config.Signaling.Region);
    }

    [Fact]
    public async Task BuildAsync_LeavesTheRegionUnsetWhenTheTenantPinsNone()
    {
        // Arrange
        // Nothing configured is the default and must stay the behaviour every client had before the setting
        // existed: Telnyx chooses, and the browser adds no region to its client options at all.
        var contributor = CreateContributor(null);

        // Act
        var config = await contributor.BuildAsync(new SoftPhoneRegistrationConfigContext
        {
            UserId = "user-1",
            DisplayName = "Agent One",
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(config.Signaling.Region);
    }

    private static TelnyxSoftPhoneRegistrationConfigContributor CreateContributor(string region)
    {
        var issuer = new Mock<ITelnyxTelephonyCredentialIssuer>();
        issuer
            .Setup(i => i.IssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelnyxTelephonyCredential
            {
                CredentialId = "credential-1",
                SipUsername = "agent-one",
                SipPassword = "secret",
                ExpiresAtUtc = new DateTime(2026, 9, 14, 12, 0, 0, DateTimeKind.Utc),
            });

        var options = new TestOptionsMonitor<TelnyxOptions>(new TelnyxOptions
        {
            IsEnabled = true,
            ApiKey = "KEY",
            ConnectionId = "connection-1",
            SipConnectionId = "sip-connection-1",
            SipWebSocketUrl = TelnyxConstants.DefaultSipWebSocketUrl,
            SipDomain = TelnyxConstants.DefaultSipDomain,
            WebRtcRegion = region,
        });

        return new TelnyxSoftPhoneRegistrationConfigContributor(issuer.Object, options);
    }
}
