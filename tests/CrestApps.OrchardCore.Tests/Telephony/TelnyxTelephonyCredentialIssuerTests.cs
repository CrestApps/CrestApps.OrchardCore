using System.Net;
using CrestApps.OrchardCore.Telnyx.Models;
using CrestApps.OrchardCore.Telnyx.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// The credential issuer was untested, and it is the component behind a live incident: an agent whose browser
/// churned credentials hit the account cap and every subsequent registration came back LOGIN_FAILED, so their
/// phone silently stopped ringing. These pin the cap, the eviction order, and the revocation behaviour that
/// keeps the local record and the provider's from drifting apart.
/// </summary>
public sealed class TelnyxTelephonyCredentialIssuerTests
{
    private static readonly DateTime _now = new(2026, 3, 4, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Issue_MintsACredentialAndStoresIt()
    {
        // Arrange
        var harness = new Harness();
        harness.Handler.RespondWith(HttpStatusCode.OK, """{"data":{"id":"cred-1","sip_username":"user-1","sip_password":"secret"}}""");

        // Act
        var credential = await harness.Issuer.IssueAsync("u1", "Agent One", TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(credential);
        Assert.Equal("user-1", credential.SipUsername);
        Assert.Single(harness.Store.Created);
        Assert.Equal("cred-1", harness.Store.Created[0].CredentialId);
    }

    [Fact]
    public async Task Issue_WhenTheProviderRefuses_StoresNothing()
    {
        // Arrange
        // A local record for a credential the provider never minted is a credential the agent can never register
        // on and nothing will ever clean up.
        var harness = new Harness();
        harness.Handler.RespondWith(HttpStatusCode.UnprocessableEntity, """{"errors":[{"detail":"no"}]}""");

        // Act
        var credential = await harness.Issuer.IssueAsync("u1", "Agent One", TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(credential);
        Assert.Empty(harness.Store.Created);
    }

    [Fact]
    public async Task Issue_AtTheCap_EvictsTheOldestCredentialFirst()
    {
        // Arrange
        // This is the incident. The account has a per-user credential cap; once it is reached Telnyx refuses to
        // mint, and the agent's next registration fails. Evicting the oldest is right because the newest is the
        // one the browser is most likely registered on right now.
        var harness = new Harness();
        harness.Store.Live.AddRange(Enumerable.Range(0, 8).Select(index => new TelnyxAgentCredential
        {
            UserId = "u1",
            CredentialId = $"cred-{index}",
            IssuedUtc = _now.AddMinutes(-index),
            ExpiresUtc = _now.AddHours(1),
        }));

        // The eviction delete and the mint both answer OK; the body only matters to the mint.
        harness.Handler.AlwaysRespondWith(HttpStatusCode.OK, """{"data":{"id":"cred-new","sip_username":"user-new","sip_password":"secret"}}""");

        // Act
        var credential = await harness.Issuer.IssueAsync("u1", "Agent One", TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(credential);

        // The oldest was the one issued furthest in the past.
        Assert.Contains(harness.Store.Revoked, revoked => revoked.CredentialId == "cred-7");
        Assert.DoesNotContain(harness.Store.Revoked, revoked => revoked.CredentialId == "cred-0");
    }

    [Fact]
    public async Task Issue_BelowTheCap_EvictsNothing()
    {
        // Arrange
        var harness = new Harness();
        harness.Store.Live.Add(new TelnyxAgentCredential
        {
            UserId = "u1",
            CredentialId = "cred-existing",
            IssuedUtc = _now.AddMinutes(-5),
            ExpiresUtc = _now.AddHours(1),
        });

        harness.Handler.RespondWith(HttpStatusCode.OK, """{"data":{"id":"cred-new","sip_username":"user-new","sip_password":"secret"}}""");

        // Act
        await harness.Issuer.IssueAsync("u1", "Agent One", TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(harness.Store.Revoked);
    }

    [Fact]
    public async Task RevokeForUser_RevokesEveryLiveCredential()
    {
        // Arrange
        // Sign-out has to leave nothing registered, or a signed-out agent keeps ringing.
        var harness = new Harness();
        harness.Store.Live.AddRange(
        [
            new TelnyxAgentCredential { UserId = "u1", CredentialId = "cred-1", IssuedUtc = _now, ExpiresUtc = _now.AddHours(1) },
            new TelnyxAgentCredential { UserId = "u1", CredentialId = "cred-2", IssuedUtc = _now, ExpiresUtc = _now.AddHours(1) },
        ]);

        harness.Handler.AlwaysRespondWith(HttpStatusCode.OK);

        // Act
        var revoked = await harness.Issuer.RevokeForUserAsync("u1", "signed out", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, revoked);
        Assert.Equal(2, harness.Store.Revoked.Count);
    }

    [Fact]
    public async Task Revoke_WhenTheProviderHasAlreadyForgottenTheCredential_StillRevokesLocally()
    {
        // Arrange
        // A 404 means the provider agrees the credential is gone. Refusing to revoke locally would leave a
        // record that is live forever, counting against the cap that caused the incident in the first place.
        var harness = new Harness();
        harness.Store.Live.Add(new TelnyxAgentCredential
        {
            UserId = "u1",
            CredentialId = "cred-gone",
            IssuedUtc = _now,
            ExpiresUtc = _now.AddHours(1),
        });

        harness.Handler.AlwaysRespondWith(HttpStatusCode.NotFound);

        // Act
        var revoked = await harness.Issuer.RevokeForUserAsync("u1", "signed out", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, revoked);
        Assert.Single(harness.Store.Revoked);
    }

    private sealed class Harness
    {
        public RecordingHttpMessageHandler Handler { get; } = new();

        public FakeCredentialStore Store { get; } = new();

        public TelnyxTelephonyCredentialIssuer Issuer { get; }

        public Harness()
        {
            var httpClient = new HttpClient(Handler)
            {
                BaseAddress = new Uri("https://api.telnyx.com/v2/"),
            };


            var clock = new Mock<IClock>();
            clock.SetupGet(value => value.UtcNow).Returns(_now);

            // The issuer refuses to mint unless the provider is fully configured, which is the behaviour a
            // tenant that has not finished setting Telnyx up depends on.
            var options = new TelnyxOptions
            {
                IsEnabled = true,
                ApiBaseUrl = "https://api.telnyx.com/v2/",
                ApiKey = "test-api-key",
                ConnectionId = "conn-1",
                SipConnectionId = "sip-conn-1",
                CredentialLifetimeMinutes = 60,
            };

            var monitor = new Mock<IOptionsMonitor<TelnyxOptions>>();
            monitor.SetupGet(value => value.CurrentValue).Returns(options);

            // The issuer now goes through the typed client, so the double sits under that rather than under a
            // hand-built HttpClient the issuer used to construct itself.
            var apiClient = new TelnyxApiClient(
                httpClient,
                new OptionsWrapper<TelnyxOptions>(options),
                new TelnyxApiRetryPolicy(TimeSpan.Zero),
                NullLogger<TelnyxApiClient>.Instance);

            Issuer = new TelnyxTelephonyCredentialIssuer(
                apiClient,
                Store,
                clock.Object,
                NullLogger<TelnyxTelephonyCredentialIssuer>.Instance,
                new Mock<ISoftPhoneHealthMetrics>().Object,
                monitor.Object);
        }
    }

    private sealed class FakeCredentialStore : ITelnyxAgentCredentialStore
    {
        public List<TelnyxAgentCredential> Live { get; } = [];

        public List<TelnyxAgentCredential> Created { get; } = [];

        public List<TelnyxAgentCredential> Revoked { get; } = [];

        public Task<IReadOnlyList<TelnyxAgentCredential>> ListLiveByUserAsync(string userId, DateTime nowUtc, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TelnyxAgentCredential>>(Live.ToArray());

        public Task CreateAsync(TelnyxAgentCredential credential, CancellationToken cancellationToken = default)
        {
            Created.Add(credential);
            Live.Add(credential);

            return Task.CompletedTask;
        }

        public Task MarkRevokedAsync(TelnyxAgentCredential credential, DateTime revokedUtc, CancellationToken cancellationToken = default)
        {
            Revoked.Add(credential);
            Live.Remove(credential);

            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<TelnyxAgentCredential>> ListByUserAsync(string userId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TelnyxAgentCredential>>(Live.ToArray());

        public Task<bool> MarkRegisteredAsync(string userId, string credentialId, DateTime registeredUtc, CancellationToken cancellationToken = default)
            => Task.FromResult(true);
    }
}
