using CrestApps.OrchardCore.Telnyx.Models;
using CrestApps.OrchardCore.Telnyx.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// Two code paths resolved an agent's SIP endpoint by taking the first live credential the store returned, which
/// is the newest issued one. A browser that had re-registered under an older credential was therefore dialled at
/// a credential nothing was listening on, and the call came back SIP 486 — the agent's phone never rang and the
/// caller heard busy. Registration, not recency, is what makes a credential reachable.
/// </summary>
public sealed class TelnyxAgentEndpointResolverTests
{
    private static readonly DateTime _now = new(2026, 3, 4, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Resolves_TheRegisteredCredential_OverANewerUnregisteredOne()
    {
        // Arrange
        // This is the incident: the newest credential had been issued but never registered, and dialling it
        // produced a busy signal on a phone that was sitting there idle under the older one.
        var resolver = Resolver(
            Credential("newer-unregistered", issuedUtc: _now, registeredUtc: null),
            Credential("older-registered", issuedUtc: _now.AddMinutes(-30), registeredUtc: _now.AddMinutes(-29)));

        // Act
        var endpoint = await resolver.ResolveAsync("u1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("sip:older-registered@sip.example.com", endpoint);
    }

    [Fact]
    public async Task Resolves_TheMostRecentlyRegistered_WhenSeveralAreRegistered()
    {
        // Arrange
        // An agent who reloaded the soft phone has more than one registered credential; the live one is the one
        // that registered last.
        var resolver = Resolver(
            Credential("registered-earlier", issuedUtc: _now.AddMinutes(-30), registeredUtc: _now.AddMinutes(-25)),
            Credential("registered-later", issuedUtc: _now.AddMinutes(-20), registeredUtc: _now.AddMinutes(-2)));

        // Act
        var endpoint = await resolver.ResolveAsync("u1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("sip:registered-later@sip.example.com", endpoint);
    }

    [Fact]
    public async Task Resolves_TheNewestIssued_WhenNothingHasRegistered()
    {
        // Arrange
        // Nothing is reachable either way, but the newest credential is the one the browser is most likely
        // registering against right now, so it is the least wrong guess.
        var resolver = Resolver(
            Credential("older", issuedUtc: _now.AddMinutes(-30), registeredUtc: null),
            Credential("newer", issuedUtc: _now.AddMinutes(-1), registeredUtc: null));

        // Act
        var endpoint = await resolver.ResolveAsync("u1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("sip:newer@sip.example.com", endpoint);
    }

    [Fact]
    public async Task Resolves_Nothing_WhenTheAgentHasNoLiveCredential()
    {
        // Arrange
        // The caller must be able to tell "this agent has no phone" from "this agent has a phone at some
        // address": the first is a routable condition, and inventing an address hides it until the call fails.
        var resolver = Resolver();

        // Act
        var endpoint = await resolver.ResolveAsync("u1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(endpoint);
    }

    [Fact]
    public async Task Resolves_Nothing_WhenTheCredentialHasNoSipUsername()
    {
        // Arrange
        var resolver = Resolver(Credential(sipUsername: null, issuedUtc: _now, registeredUtc: _now));

        // Act
        var endpoint = await resolver.ResolveAsync("u1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(endpoint);
    }

    [Fact]
    public async Task Resolves_Nothing_ForAnEmptyUser()
    {
        // Arrange
        var resolver = Resolver(Credential("registered", issuedUtc: _now, registeredUtc: _now));

        // Act
        var endpoint = await resolver.ResolveAsync("   ", TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(endpoint);
    }

    [Fact]
    public async Task ResolveRedelivery_MarksTheRefusedCredentialUnreachable_AndResolvesTheCredentialThePhoneMovedTo()
    {
        // Arrange
        var stale = Credential("stale", issuedUtc: _now.AddMinutes(-30), registeredUtc: _now.AddMinutes(-29));
        var fresh = Credential("fresh", issuedUtc: _now.AddSeconds(-2), registeredUtc: null);
        var store = Store(stale, fresh);
        var resolver = Resolver(store);

        // Act
        var redelivery = await resolver.ResolveRedeliveryAsync("u1", "sip:stale@sip.example.com", null, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(redelivery);
        Assert.Equal("sip:fresh@sip.example.com", redelivery.Endpoint);
        Assert.Equal("fresh", redelivery.CredentialId);
        Assert.Equal("stale", redelivery.UnreachableCredentialId);
        store.Verify(value => value.MarkUnreachableAsync("u1", "stale", _now, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResolveRedelivery_WhenTheRefusedCredentialIsTheOnlyOne_ResolvesNothing()
    {
        // Arrange
        var resolver = Resolver(Store(Credential("stale", issuedUtc: _now.AddMinutes(-30), registeredUtc: _now.AddMinutes(-29))));

        // Act
        var redelivery = await resolver.ResolveRedeliveryAsync("u1", "sip:stale@sip.example.com", null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(redelivery);
    }

    [Fact]
    public async Task ResolveRedelivery_WhenTheOtherCredentialsClientLacksTheCapability_ResolvesNothing()
    {
        // Arrange
        var resolver = Resolver(Store(
            Credential("stale", issuedUtc: _now.AddMinutes(-30), registeredUtc: _now.AddMinutes(-29)),
            Credential("fresh", issuedUtc: _now.AddSeconds(-2), registeredUtc: _now.AddSeconds(-1))));

        // Act
        var redelivery = await resolver.ResolveRedeliveryAsync("u1", "sip:stale@sip.example.com", "held-offer-leg", TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(redelivery);
    }

    private static Mock<ITelnyxAgentCredentialStore> Store(params TelnyxAgentCredential[] credentials)
    {
        var store = new Mock<ITelnyxAgentCredentialStore>();
        store
            .Setup(value => value.ListLiveByUserAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(credentials);
        store
            .Setup(value => value.MarkUnreachableAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string sipUsername, DateTime unreachableUtc, CancellationToken _) =>
            {
                var credential = credentials.FirstOrDefault(candidate => candidate.SipUsername == sipUsername);

                if (credential is not null)
                {
                    credential.UnreachableUtc = unreachableUtc;
                }

                return credential;
            });

        return store;
    }

    private static TelnyxAgentEndpointResolver Resolver(Mock<ITelnyxAgentCredentialStore> store)
        => new(
            store.Object,
            new OptionsWrapper<TelnyxOptions>(new TelnyxOptions { SipDomain = "sip.example.com" }),
            new StubClock(_now),
            NullLogger<TelnyxAgentEndpointResolver>.Instance);

    private static TelnyxAgentCredential Credential(string sipUsername, DateTime issuedUtc, DateTime? registeredUtc)
        => new()
        {
            UserId = "u1",
            CredentialId = sipUsername ?? "no-username",
            SipUsername = sipUsername,
            IssuedUtc = issuedUtc,
            ExpiresUtc = issuedUtc.AddHours(1),
            RegisteredUtc = registeredUtc,
        };

    private static TelnyxAgentEndpointResolver Resolver(params TelnyxAgentCredential[] credentials)
    {
        var store = new Mock<ITelnyxAgentCredentialStore>();
        store
            .Setup(value => value.ListLiveByUserAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(credentials);

        return new TelnyxAgentEndpointResolver(
            store.Object,
            new OptionsWrapper<TelnyxOptions>(new TelnyxOptions { SipDomain = "sip.example.com" }),
            new StubClock(_now),
            NullLogger<TelnyxAgentEndpointResolver>.Instance);
    }
}
