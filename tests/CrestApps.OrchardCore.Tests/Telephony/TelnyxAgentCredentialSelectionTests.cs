using CrestApps.OrchardCore.Telnyx.Models;
using CrestApps.OrchardCore.Telnyx.Services;

namespace CrestApps.OrchardCore.Tests.Telephony;

public sealed class TelnyxAgentCredentialSelectionTests
{
    private static readonly DateTime _now = new(2026, 8, 28, 21, 56, 0, DateTimeKind.Utc);

    [Fact]
    public void OrderByDeliveryPreference_WhenANewerCredentialWasNeverRegistered_PrefersTheRegisteredOne()
    {
        // Arrange
        // The exact shape of the live failure: the client registered on the credential it minted first, then a
        // second credential was minted whose registration never completed. Both are live, and picking the
        // newest-issued one sends the agent's leg to an endpoint no client is registered on, which Telnyx
        // refuses with SIP 486 -- the agent never rings and the customer hears nothing.
        var registered = new TelnyxAgentCredential
        {
            CredentialId = "credential-a",
            SipUsername = "gencredA",
            IssuedUtc = _now,
            RegisteredUtc = _now.AddSeconds(5),
            ExpiresUtc = _now.AddHours(1),
        };

        var mintedButNeverRegistered = new TelnyxAgentCredential
        {
            CredentialId = "credential-b",
            SipUsername = "gencredB",
            IssuedUtc = _now.AddSeconds(16),
            RegisteredUtc = null,
            ExpiresUtc = _now.AddHours(1),
        };

        // Act
        var ordered = TelnyxAgentCredentialSelection.OrderByDeliveryPreference([mintedButNeverRegistered, registered]);

        // Assert
        Assert.Equal("credential-a", ordered[0].CredentialId);
    }

    [Fact]
    public void OrderByDeliveryPreference_WhenSeveralWereRegistered_PrefersTheMostRecentlyRegistered()
    {
        // Arrange
        // A client that re-registers reports each time. The latest report describes where the client is now.
        var older = new TelnyxAgentCredential
        {
            CredentialId = "credential-a",
            IssuedUtc = _now,
            RegisteredUtc = _now.AddSeconds(5),
            ExpiresUtc = _now.AddHours(1),
        };

        var newer = new TelnyxAgentCredential
        {
            CredentialId = "credential-b",
            IssuedUtc = _now.AddSeconds(16),
            RegisteredUtc = _now.AddSeconds(20),
            ExpiresUtc = _now.AddHours(1),
        };

        // Act
        var ordered = TelnyxAgentCredentialSelection.OrderByDeliveryPreference([older, newer]);

        // Assert
        Assert.Equal("credential-b", ordered[0].CredentialId);
    }

    [Fact]
    public void OrderByDeliveryPreference_WhenNothingWasEverRegistered_FallsBackToNewestIssued()
    {
        // Arrange
        // A client that predates the registration report never tells the server where it registered, so the
        // previous behaviour has to remain the fallback rather than resolving to nothing.
        var older = new TelnyxAgentCredential
        {
            CredentialId = "credential-a",
            IssuedUtc = _now,
            ExpiresUtc = _now.AddHours(1),
        };

        var newer = new TelnyxAgentCredential
        {
            CredentialId = "credential-b",
            IssuedUtc = _now.AddSeconds(16),
            ExpiresUtc = _now.AddHours(1),
        };

        // Act
        var ordered = TelnyxAgentCredentialSelection.OrderByDeliveryPreference([older, newer]);

        // Assert
        Assert.Equal("credential-b", ordered[0].CredentialId);
    }
}
