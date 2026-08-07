using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.PhoneNumbers;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Pins the one definition of which external destinations the platform is willing to call.
/// <para>
/// The question was previously answered by three separate copies of the same code — one in the settings
/// driver that decides whether a destination may be saved, one in the transfer resolver that decides whether
/// a live call may be handed over, and one in the dial executor that decides whether a command may run. Three
/// copies of a safety rule drift, and when they drift the disagreement shows up as a destination that the
/// settings screen refuses but a workflow can still reach.
/// </para>
/// </summary>
public sealed class ExternalDestinationPolicyTests
{
    [Theory]
    [InlineData("+14255551212")]
    [InlineData("+442071838750")]
    [InlineData("+81312345678")]
    public void ADestination_IsAllowed_WhenItIsAnOrdinaryE164Number(string address)
    {
        // Assert
        Assert.True(ExternalDestinationPolicy.IsAllowed(address));
        Assert.True(ExternalDestinationPolicy.IsAllowed(PhoneNumber.FromE164(address)));
    }

    [Theory]
    [InlineData("911")]
    [InlineData("112")]
    [InlineData("999")]
    [InlineData("+1911")]
    [InlineData("+14255550911")]
    [InlineData("+14255550112")]
    [InlineData("+14255550999")]
    public void ADestination_IsRefused_WhenItReachesAnEmergencyService(string address)
    {
        // Assert
        Assert.False(ExternalDestinationPolicy.IsAllowed(address));
    }

    [Theory]
    [InlineData("+19001234567")]
    [InlineData("+19761234567")]
    [InlineData("+44701234567")]
    public void ADestination_IsRefused_WhenItReachesAPremiumRateService(string address)
    {
        // Assert
        Assert.False(ExternalDestinationPolicy.IsAllowed(address));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("4255551212")]
    [InlineData("+142555")]
    [InlineData("+1425555121212345")]
    [InlineData("+1 425 555 1212")]
    [InlineData("sip:+14255551212@pbx.example.com")]
    public void ADestination_IsRefused_WhenItIsNotAnE164NumberOfDialableLength(string address)
    {
        // Assert
        // The policy refuses rather than repairs. An address that arrived in some other shape was produced by
        // a path that did not canonicalize it, and guessing what was meant is how a call ends up somewhere
        // nobody asked for.
        Assert.False(ExternalDestinationPolicy.IsAllowed(address));
    }

    [Fact]
    public void ADefaultNumber_IsRefused()
    {
        // Assert
        Assert.False(ExternalDestinationPolicy.IsAllowed(default(PhoneNumber)));
    }
}
