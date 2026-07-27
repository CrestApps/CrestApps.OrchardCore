using CrestApps.OrchardCore.Asterisk;
using CrestApps.OrchardCore.Asterisk.Models;
using CrestApps.OrchardCore.Asterisk.Services;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Telephony;

public sealed class AsteriskSoftPhoneRegistrationConfigContributorTests
{
    /// <summary>
    /// The coturn shared secret this repository ships for local development, read from the asset itself rather
    /// than copied here, so the test proves the guard rejects the exact value that is published.
    /// </summary>
    private static string PublishedDevelopmentTurnSecret => ReadDevelopmentTurnSecret();

    [Fact]
    public async Task BuildAsync_WhenDefaultProviderConfigured_ReturnsContractShapeWithTurnCredentials()
    {
        // Arrange
        var expiresAtUtc = new DateTime(2026, 7, 16, 12, 15, 0, DateTimeKind.Utc);
        var issuer = new TestCredentialIssuer(expiresAtUtc);
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(new DateTime(2026, 7, 16, 12, 0, 0, DateTimeKind.Utc));
        var contributor = CreateContributor(
            new DefaultAsteriskOptions
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
            },
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
        var contributor = CreateContributor(
            new DefaultAsteriskOptions { IsEnabled = true },
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

    [Fact]
    public async Task BuildAsync_InProduction_WhenTurnSecretIsPublishedInThisRepository_IssuesNoRelayCredential()
    {
        // Arrange
        var expiresAtUtc = new DateTime(2026, 7, 16, 12, 15, 0, DateTimeKind.Utc);
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(new DateTime(2026, 7, 16, 12, 0, 0, DateTimeKind.Utc));

        var contributor = CreateContributor(
            CreateWebRtcOptions(PublishedDevelopmentTurnSecret),
            new TestCredentialIssuer(expiresAtUtc),
            clock.Object,
            Environments.Production);

        // Act
        var config = await contributor.BuildAsync(new SoftPhoneRegistrationConfigContext
        {
            ProviderName = AsteriskConstants.DefaultProviderTechnicalName,
            UserId = "user-1",
            DisplayName = "Agent One",
            InteractionId = "interaction-1",
        }, TestContext.Current.CancellationToken);

        // Assert
        var iceServer = Assert.Single(config.Ice.IceServers);

        // The URLs are still advertised so STUN keeps working; only the relay credential derived from a
        // published secret is withheld.
        Assert.NotEmpty(iceServer.Urls);
        Assert.Null(iceServer.Username);
        Assert.Null(iceServer.Credential);
    }

    [Fact]
    public async Task BuildAsync_InDevelopment_WhenTurnSecretIsPublishedInThisRepository_StillIssuesTheRelayCredential()
    {
        // Arrange
        var expiresAtUtc = new DateTime(2026, 7, 16, 12, 15, 0, DateTimeKind.Utc);
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(new DateTime(2026, 7, 16, 12, 0, 0, DateTimeKind.Utc));

        var contributor = CreateContributor(
            CreateWebRtcOptions(PublishedDevelopmentTurnSecret),
            new TestCredentialIssuer(expiresAtUtc),
            clock.Object,
            Environments.Development);

        // Act
        var config = await contributor.BuildAsync(new SoftPhoneRegistrationConfigContext
        {
            ProviderName = AsteriskConstants.DefaultProviderTechnicalName,
            UserId = "user-1",
            DisplayName = "Agent One",
            InteractionId = "interaction-1",
        }, TestContext.Current.CancellationToken);

        // Assert
        var iceServer = Assert.Single(config.Ice.IceServers);

        // The sample secret exists so the Aspire stack runs without an operator inventing one. Rejecting it
        // outside production would break the workflow the guard was written to protect.
        Assert.NotEmpty(iceServer.Credential);
    }

    private static DefaultAsteriskOptions CreateWebRtcOptions(string turnSharedSecret)
        => new()
        {
            IsEnabled = true,
            WebSocketUrl = "wss://pbx.example.test/ws",
            SipDomain = "pbx.example.test",
            TurnUrls = "turn:turn.example.test:3478",
            TurnSharedSecret = turnSharedSecret,
            IceTransportPolicy = "relay",
            WebRtcCodecs = "opus",
            PjsipCredentialLifetimeMinutes = 15,
            PjsipContactExpirationSeconds = 120,
            PjsipRealtimeProviderInvariantName = "Microsoft.Data.Sqlite",
            PjsipRealtimeConnectionString = "Data Source=asterisk.db",
        };

    private static AsteriskSoftPhoneRegistrationConfigContributor CreateContributor(        DefaultAsteriskOptions options,
        IAsteriskPjsipCredentialIssuer issuer,
        IClock clock,
        string environmentName = null)
        => new(
            SiteServiceFactory.Create(new AsteriskSettings()),
            Mock.Of<IDataProtectionProvider>(),
            Options.Create(options),
            issuer,
            clock,
            Mock.Of<IHostEnvironment>(environment =>
                environment.EnvironmentName == (environmentName ?? Environments.Development)),
            NullLogger<AsteriskSoftPhoneRegistrationConfigContributor>.Instance);

    private static string ReadDevelopmentTurnSecret()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CrestApps.OrchardCore.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);

        var path = Path.Combine(
            directory.FullName,
            "src",
            "Startup",
            "CrestApps.Aspire.AppHost",
            "Coturn",
            "turnserver.conf");

        Assert.True(File.Exists(path), $"The development coturn profile was not found at '{path}'.");

        var line = File.ReadLines(path)
            .FirstOrDefault(candidate => candidate.StartsWith("static-auth-secret=", StringComparison.Ordinal));

        Assert.NotNull(line);

        return line.Substring("static-auth-secret=".Length).Trim();
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
