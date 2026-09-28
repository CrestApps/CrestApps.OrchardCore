using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.PhoneNumbers;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Pins the behavior of the obsolete <see cref="ExternalDestinationPolicy"/> forwarder while it is kept for one
/// release. The authority is now <c>IDialDestinationPolicy</c> in Telephony, covered by
/// <c>DefaultDialDestinationPolicyTests</c>; these tests exist so the retained type cannot silently disagree
/// with it in the meantime.
/// </summary>
#pragma warning disable CS0618 // The type under test is deliberately obsolete and still shipped.
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
    [InlineData("+14255550911")]
    [InlineData("+14255550112")]
    [InlineData("+14255550999")]
    public void ADestination_IsAllowed_WhenItMerelyEndsInAnEmergencyCode(string address)
    {
        // Assert
        // An emergency code is the whole dialed string. Matching it as a suffix refused every ordinary number
        // whose last three digits happened to look like one, which is a large slice of real destinations.
        Assert.True(ExternalDestinationPolicy.IsAllowed(address));
    }

    [Theory]
    [InlineData("911")]
    [InlineData("112")]
    [InlineData("999")]
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
#pragma warning restore CS0618
