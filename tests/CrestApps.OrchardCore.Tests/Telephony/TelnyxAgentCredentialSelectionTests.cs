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

    [Fact]
    public void OrderByDeliveryPreference_KeepsEveryCredential_SoACallerCanFallThrough()
    {
        // Arrange
        // The caller walks the ordering looking for the first usable credential, so dropping the ones that rank
        // badly would leave it with nothing to fall through to when the best one turns out to be unusable.
        var credentials = new[]
        {
            new TelnyxAgentCredential { CredentialId = "a", IssuedUtc = _now, ExpiresUtc = _now.AddHours(1) },
            new TelnyxAgentCredential { CredentialId = "b", IssuedUtc = _now.AddSeconds(10), RegisteredUtc = _now.AddSeconds(11), ExpiresUtc = _now.AddHours(1) },
            new TelnyxAgentCredential { CredentialId = "c", IssuedUtc = _now.AddSeconds(20), ExpiresUtc = _now.AddHours(1) },
        };

        // Act
        var ordered = TelnyxAgentCredentialSelection.OrderByDeliveryPreference(credentials);

        // Assert
        Assert.Equal(3, ordered.Count);
    }

    [Fact]
    public void OrderByDeliveryPreference_ACredentialFoundUnreachable_RanksAfterEveryOther()
    {
        // Arrange
        // A phone that reopened left its old credential reading as registered, and a leg to it came back SIP 480. Until
        // something registers on it again, even a credential nothing has registered on yet is the better guess.
        var refused = new TelnyxAgentCredential
        {
            CredentialId = "refused",
            IssuedUtc = _now,
            RegisteredUtc = _now.AddSeconds(5),
            RegisteredConnectionId = "connection-a",
            UnreachableUtc = _now.AddMinutes(30),
            ExpiresUtc = _now.AddHours(1),
        };

        var fresh = new TelnyxAgentCredential
        {
            CredentialId = "fresh",
            IssuedUtc = _now.AddMinutes(30),
            ExpiresUtc = _now.AddHours(1),
        };

        // Act
        var ordered = TelnyxAgentCredentialSelection.OrderByDeliveryPreference([refused, fresh]);

        // Assert
        Assert.Equal("fresh", ordered[0].CredentialId);
        Assert.Equal(2, ordered.Count);
    }

    [Fact]
    public void OrderForRedelivery_LeavesOutUnreachableCredentials_AndPrefersTheOneMintedAfterTheOthersWereLastSeen()
    {
        // Arrange
        // The live shape: the phone reopened and minted a credential it is registering on right now, while the window it
        // had before closed half an hour earlier. The retry must go to the new credential, not back to the closed one.
        var closedEarlier = new TelnyxAgentCredential
        {
            CredentialId = "closed-earlier",
            IssuedUtc = _now,
            RegisteredUtc = _now.AddSeconds(5),
            RegisteredConnectionId = "connection-a",
            ConnectionClosedUtc = _now.AddMinutes(1),
            ExpiresUtc = _now.AddHours(1),
        };

        var refused = new TelnyxAgentCredential
        {
            CredentialId = "refused",
            IssuedUtc = _now.AddMinutes(2),
            RegisteredUtc = _now.AddMinutes(2),
            RegisteredConnectionId = "connection-b",
            UnreachableUtc = _now.AddMinutes(31),
            ExpiresUtc = _now.AddHours(1),
        };

        var justMinted = new TelnyxAgentCredential
        {
            CredentialId = "just-minted",
            IssuedUtc = _now.AddMinutes(30),
            ExpiresUtc = _now.AddHours(1),
        };

        // Act
        var ordered = TelnyxAgentCredentialSelection.OrderForRedelivery([closedEarlier, refused, justMinted]);

        // Assert
        Assert.Equal(["just-minted", "closed-earlier"], ordered.Select(credential => credential.CredentialId));
    }

    [Fact]
    public void OrderForRedelivery_StillPrefersACredentialRegisteredByAnOpenWindow()
    {
        // Arrange
        var open = new TelnyxAgentCredential
        {
            CredentialId = "open",
            IssuedUtc = _now,
            RegisteredUtc = _now.AddSeconds(5),
            RegisteredConnectionId = "connection-a",
            ExpiresUtc = _now.AddHours(1),
        };

        var newerUnregistered = new TelnyxAgentCredential
        {
            CredentialId = "newer-unregistered",
            IssuedUtc = _now.AddMinutes(10),
            ExpiresUtc = _now.AddHours(1),
        };

        // Act
        var ordered = TelnyxAgentCredentialSelection.OrderForRedelivery([newerUnregistered, open]);

        // Assert
        Assert.Equal("open", ordered[0].CredentialId);
    }

    [Fact]
    public void OrderByDeliveryPreference_OfNothing_IsEmptyRatherThanNull()
    {
        // Assert
        // The result is enumerated directly on a path that runs while a customer is holding the line, so a null
        // here is an exception thrown at somebody waiting to be connected.
        Assert.Empty(TelnyxAgentCredentialSelection.OrderByDeliveryPreference(null));
        Assert.Empty(TelnyxAgentCredentialSelection.OrderByDeliveryPreference([]));
    }
}
