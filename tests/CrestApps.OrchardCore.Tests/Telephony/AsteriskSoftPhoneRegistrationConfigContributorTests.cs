using CrestApps.OrchardCore.Asterisk;
using CrestApps.OrchardCore.Asterisk.Models;
using CrestApps.OrchardCore.Asterisk.Services;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Telephony;

public sealed class AsteriskSoftPhoneRegistrationConfigContributorTests
{
    [Fact]
    public async Task BuildAsync_WhenDefaultProviderConfigured_ReturnsContractShapeWithTurnCredentials()
    {
        // Arrange
        var expiresAtUtc = new DateTime(2026, 7, 16, 12, 15, 0, DateTimeKind.Utc);
        var issuer = new TestCredentialIssuer(expiresAtUtc);
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(new DateTime(2026, 7, 16, 12, 0, 0, DateTimeKind.Utc));
        var contributor = new AsteriskSoftPhoneRegistrationConfigContributor(
            SiteServiceFactory.Create(new AsteriskSettings()),
            Mock.Of<IDataProtectionProvider>(),
            Options.Create(new DefaultAsteriskOptions
            {
                IsEnabled = true,
                WebSocketUrl = "wss://pbx.example.test/ws",
                SipDomain = "pbx.example.test",
                TurnUrls = "turn:turn.example.test:3478\nstun:turn.example.test:3478",
                TurnSharedSecret = "turn-secret",
                IceTransportPolicy = "relay",
                WebRtcCodecs = "opus,g722,ulaw",
                PjsipCredentialLifetimeMinutes = 15,
                PjsipContactExpirationSeconds = 120,
                PjsipRealtimeProviderInvariantName = "Microsoft.Data.Sqlite",
                PjsipRealtimeConnectionString = "Data Source=asterisk.db",
            }),
            issuer,
            clock.Object);

        // Act
        var config = await contributor.BuildAsync(new SoftPhoneRegistrationConfigContext
        {
            ProviderName = AsteriskConstants.DefaultProviderTechnicalName,
            UserId = "user-1",
            DisplayName = "Agent One",
            InteractionId = "interaction-1",
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(config);
        Assert.Equal(AsteriskConstants.DefaultProviderTechnicalName, config.Provider);
        Assert.Equal("wss://pbx.example.test/ws", config.Signaling.WebSocketUrl);
        Assert.Equal("sip:cc-tenanta-credential@pbx.example.test", config.Signaling.SipUri);
        Assert.Equal("cc-tenanta-credential", config.Signaling.AuthorizationUser);
        Assert.Equal("password", config.Credential.Type);
        Assert.Equal("secret", config.Credential.Value);
        Assert.Equal(expiresAtUtc, config.Credential.ExpiresAtUtc);
        Assert.Equal("relay", config.Ice.IceTransportPolicy);
        Assert.Equal(new[] { "opus", "g722", "ulaw" }, config.Media.Codecs);
        Assert.Equal("server-session", config.Session.InteractionId);
        Assert.Single(config.Ice.IceServers);
        Assert.EndsWith(":TenantA:server-session", config.Ice.IceServers[0].Username, StringComparison.Ordinal);
        Assert.NotEmpty(config.Ice.IceServers[0].Credential);

        // The caller-supplied interaction id is carried only as non-authoritative metadata and never
        // becomes the server-owned session identity.
        Assert.Equal("interaction-1", issuer.LastRequest.InteractionId);
        Assert.Null(issuer.LastRequest.SessionId);
    }

    [Fact]
    public async Task BuildAsync_WhenWebRtcSettingsMissing_ReturnsNull()
    {
        // Arrange
        var contributor = new AsteriskSoftPhoneRegistrationConfigContributor(
            SiteServiceFactory.Create(new AsteriskSettings()),
            Mock.Of<IDataProtectionProvider>(),
            Options.Create(new DefaultAsteriskOptions { IsEnabled = true }),
            new TestCredentialIssuer(new DateTime(2026, 7, 16, 12, 15, 0, DateTimeKind.Utc)),
            Mock.Of<IClock>());

        // Act
        var config = await contributor.BuildAsync(new SoftPhoneRegistrationConfigContext
        {
            ProviderName = AsteriskConstants.DefaultProviderTechnicalName,
            UserId = "user-1",
            DisplayName = "Agent One",
            InteractionId = "interaction-1",
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(config);
    }

    private sealed class TestCredentialIssuer : IAsteriskPjsipCredentialIssuer
    {
        private readonly DateTime _expiresAtUtc;

        public TestCredentialIssuer(DateTime expiresAtUtc)
        {
            _expiresAtUtc = expiresAtUtc;
        }

        public AsteriskPjsipCredentialIssueRequest LastRequest { get; private set; }

        public Task<AsteriskPjsipCredential> IssueAsync(
            AsteriskPjsipCredentialIssueRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;

            // Simulate the real issuer minting a server-owned session id rather than trusting the caller.
            return Task.FromResult(new AsteriskPjsipCredential
            {
                TenantName = "TenantA",
                SessionId = "server-session",
                EndpointName = "cc-tenanta-credential",
                AuthorizationUser = "cc-tenanta-credential",
                Password = "secret",
                SipUri = "sip:cc-tenanta-credential@" + request.SipDomain,
                ExpiresAtUtc = _expiresAtUtc,
            });
        }

        public Task<AsteriskPjsipCredential> RotateAsync(
            AsteriskPjsipCredentialIssueRequest request,
            CancellationToken cancellationToken = default)
            => IssueAsync(request, cancellationToken);

        public Task<bool> RevokeAsync(
            string authorizationUser,
            string reason,
            CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<int> RevokeUserAsync(
            string userId,
            string reason,
            CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<int> CleanupExpiredAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(0);
    }
}
