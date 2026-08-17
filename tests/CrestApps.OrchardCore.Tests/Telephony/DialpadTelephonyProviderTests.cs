using System.Net;
using CrestApps.OrchardCore.Dialpad;
using CrestApps.OrchardCore.Dialpad.Models;
using CrestApps.OrchardCore.Dialpad.Services;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CrestApps.OrchardCore.Tests.Telephony;

public sealed class DialpadTelephonyProviderTests
{
    private const string PlainToken = "secret-token";
    private const string BaseUrl = "https://example.test/api/";

    [Fact]
    public async Task DialAsync_WhenConfigured_PostsToDialpadApiWithBearerToken()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "{\"id\": 12345}");
        var provider = CreateProvider(handler, out _, isEnabled: true);

        // Act
        var result = await provider.DialAsync(new DialRequest { To = "+15551234567" }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.NotNull(result.Call);
        Assert.Equal("12345", result.Call.CallId);
        Assert.Equal(CallState.Connecting, result.Call.State);
        Assert.Equal(CallDirection.Outbound, result.Call.Direction);

        Assert.Equal(HttpMethod.Post, handler.LastRequest.Method);
        Assert.Equal($"{BaseUrl}call", handler.LastRequest.RequestUri.AbsoluteUri);
        Assert.Equal("Bearer", handler.LastRequest.Headers.Authorization.Scheme);
        Assert.Equal(PlainToken, handler.LastRequest.Headers.Authorization.Parameter);
        Assert.Contains("phone_number", handler.LastRequestBody);
        Assert.Contains("15551234567", handler.LastRequestBody);
    }

    [Fact]
    public async Task DialAsync_WithIdempotencyMetadata_SendsProviderIdempotencyHeader()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "{\"id\": 12345}");
        var provider = CreateProvider(handler, out _, isEnabled: true);
        var request = new DialRequest
        {
            To = "+15551234567",
            Metadata = new Dictionary<string, string>
            {
                [TelephonyConstants.RequestMetadata.IdempotencyKey] = "command-1",
            },
        };

        // Act
        var result = await provider.DialAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.True(handler.LastRequest.Headers.TryGetValues("Idempotency-Key", out var values));
        Assert.Equal("command-1", Assert.Single(values));
    }

    [Fact]
    public async Task DialAsync_WhenTransportOutcomeIsUnknown_ReturnsOutcomeUnknown()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(_ => throw new HttpRequestException("connection lost"));
        var provider = CreateProvider(handler, out _, isEnabled: true);

        // Act
        var result = await provider.DialAsync(
            new DialRequest { To = "+15551234567" },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.True(result.OutcomeUnknown);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    public async Task DialAsync_WhenApiResponseIsAmbiguous_ReturnsOutcomeUnknown(HttpStatusCode statusCode)
    {
        // Arrange
        var handler = new StubHttpMessageHandler(statusCode, "{\"error\": \"temporary\"}");
        var provider = CreateProvider(handler, out _, isEnabled: true);

        // Act
        var result = await provider.DialAsync(
            new DialRequest { To = "+15551234567" },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.True(result.OutcomeUnknown);
    }

    [Fact]
    public async Task DialAsync_WhenDisabled_ReturnsFailedAndDoesNotCallApi()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "{\"id\": 1}");
        var provider = CreateProvider(handler, out _, isEnabled: false);

        // Act
        var result = await provider.DialAsync(new DialRequest { To = "+15551234567" }, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task DialAsync_WhenAuthenticationTypeNotSelected_ReturnsFailedAndDoesNotCallApi()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "{\"id\": 1}");
        var settings = new DialpadSettings
        {
            IsEnabled = true,
            Production = new DialpadEnvironmentSettings
            {
                AuthenticationType = DialpadAuthenticationType.NotConfigured,
                ApiBaseUrl = BaseUrl,
            },
        };

        var provider = new DialpadTelephonyProvider(
            SiteServiceFactory.Create(settings),
            new EphemeralDataProtectionProvider(),
            new StubHttpClientFactory(handler),
            Mock.Of<ITelephonyAuthenticationService>(),
            new StubClock(),
            NullLogger<DialpadTelephonyProvider>.Instance,
            new PassThroughStringLocalizer<DialpadTelephonyProvider>());

        // Act
        var result = await provider.DialAsync(new DialRequest { To = "+15551234567" }, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task DialAsync_WithoutDestination_ReturnsFailed()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "{\"id\": 1}");
        var provider = CreateProvider(handler, out _, isEnabled: true);

        // Act
        var result = await provider.DialAsync(new DialRequest { To = "" }, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task DialAsync_WhenApiReturnsError_ReturnsFailed()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(HttpStatusCode.BadRequest, "{\"error\": \"bad\"}");
        var provider = CreateProvider(handler, out _, isEnabled: true);

        // Act
        var result = await provider.DialAsync(new DialRequest { To = "+15551234567" }, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.NotNull(handler.LastRequest);
    }

    [Fact]
    public async Task HangupAsync_PostsToHangupEndpoint()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK);
        var provider = CreateProvider(handler, out _, isEnabled: true);

        // Act
        var result = await provider.HangupAsync(new CallReference { CallId = "call-1" }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(CallState.Disconnected, result.Call.State);
        Assert.Equal($"{BaseUrl}call/call-1/hangup", handler.LastRequest.RequestUri.AbsoluteUri);
    }

    [Fact]
    public async Task HoldAsync_MarksCallOnHold()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK);
        var provider = CreateProvider(handler, out _, isEnabled: true);

        // Act
        var result = await provider.HoldAsync(new CallReference { CallId = "call-1" }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.True(result.Call.IsOnHold);
        Assert.Equal(CallState.OnHold, result.Call.State);
        Assert.Equal($"{BaseUrl}call/call-1/hold", handler.LastRequest.RequestUri.AbsoluteUri);
    }

    [Fact]
    public async Task MergeAsync_WithMultipleCalls_MergesEachAdditionalCallIntoPrimary()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK);
        var provider = CreateProvider(handler, out _, isEnabled: true);

        // Act
        var result = await provider.MergeAsync(
            new MergeRequest
            {
                CallIds = ["call-1", "call-2", "call-3"],
            },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(2, handler.Requests.Count);
        Assert.All(
            handler.Requests,
            request => Assert.Equal($"{BaseUrl}call/call-1/merge", request.RequestUri.AbsoluteUri));
        Assert.True((bool)result.Call.Metadata["isConference"]);
        Assert.Equal(3, result.Call.Metadata["participantCount"]);
    }

    [Fact]
    public async Task GetClientCredentialsAsync_WhenConfigured_ReturnsProviderName()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK);
        var provider = CreateProvider(handler, out _, isEnabled: true);

        // Act
        var credentials = await provider.GetClientCredentialsAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(credentials);
        Assert.Equal(DialpadConstants.ProviderTechnicalName, credentials.ProviderName);
        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task GetDirectoryAsync_WhenConfigured_MapsDialpadUsers()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(
            HttpStatusCode.OK,
            """
            {
              "items": [
                {
                  "id": 123,
                  "first_name": "Alex",
                  "last_name": "Agent",
                  "email": "alex@example.test",
                  "extension": "2001",
                  "phone_number": "+15550002001"
                }
              ]
            }
            """);
        var provider = CreateProvider(handler, out _, isEnabled: true);

        // Act
        var result = await provider.GetDirectoryAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        var entry = Assert.Single(result.Entries);
        Assert.Equal("123", entry.Id);
        Assert.Equal("Alex Agent", entry.DisplayName);
        Assert.Equal("2001", entry.Destination);
        Assert.Equal("+15550002001", entry.PhoneNumber);
        Assert.Equal($"{BaseUrl}users", handler.LastRequest.RequestUri.AbsoluteUri);
    }

    private static DialpadTelephonyProvider CreateProvider(StubHttpMessageHandler handler, out IDataProtectionProvider dataProtectionProvider, bool isEnabled)
    {
        dataProtectionProvider = new EphemeralDataProtectionProvider();

        var protectedToken = dataProtectionProvider
            .CreateProtector(DialpadConstants.ProtectorName)
            .Protect(PlainToken);

        var settings = new DialpadSettings
        {
            IsEnabled = isEnabled,
            Production = new DialpadEnvironmentSettings
            {
                ApiToken = protectedToken,
                ApiBaseUrl = BaseUrl,
                OutboundCallerId = "+15550000000",
                UserId = "user-1",
            },
        };

        return new DialpadTelephonyProvider(
            SiteServiceFactory.Create(settings),
            dataProtectionProvider,
            new StubHttpClientFactory(handler),
            Mock.Of<ITelephonyAuthenticationService>(),
            new StubClock(),
            NullLogger<DialpadTelephonyProvider>.Instance,
            new PassThroughStringLocalizer<DialpadTelephonyProvider>());
    }

    [Fact]
    public async Task GetAuthorizationUrlAsync_BuildsUrlWithParameters()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK);
        var provider = CreateOAuthProvider(handler);

        // Act
        var url = await provider.GetAuthorizationUrlAsync(
            new TelephonyAuthorizationContext { RedirectUri = "https://site.test/cb", State = "xyz" },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.StartsWith("https://dialpad.com/oauth2/authorize", url);
        Assert.Contains("client_id=client-id", url);
        Assert.Contains("response_type=code", url);
        Assert.Contains("state=xyz", url);
        Assert.Contains("scope=calls", url);
        Assert.DoesNotContain("offline_access", url);
    }

    [Fact]
    public async Task GetAuthorizationUrlAsync_WhenOfflineAccessConfigured_IncludesOfflineAccessScope()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK);
        var provider = CreateOAuthProvider(handler, scopes: "calls:list offline_access");

        // Act
        var url = await provider.GetAuthorizationUrlAsync(
            new TelephonyAuthorizationContext { RedirectUri = "https://site.test/cb", State = "xyz" },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("offline_access", url);
    }

    [Fact]
    public async Task GetAuthorizationUrlAsync_WhenScopesNotConfigured_OmitsScopeParameter()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK);
        var provider = CreateOAuthProvider(handler, scopes: null);

        // Act
        var url = await provider.GetAuthorizationUrlAsync(
            new TelephonyAuthorizationContext { RedirectUri = "https://site.test/cb", State = "xyz" },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.DoesNotContain("scope=", url);
        Assert.DoesNotContain("offline_access", url);
    }

    [Fact]
    public async Task GetAuthorizationUrlAsync_WhenCodeChallengeProvided_IncludesPkceParameters()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK);
        var provider = CreateOAuthProvider(handler);

        // Act
        var url = await provider.GetAuthorizationUrlAsync(
            new TelephonyAuthorizationContext
            {
                RedirectUri = "https://site.test/cb",
                State = "xyz",
                CodeChallenge = "challenge-value",
                CodeChallengeMethod = "S256",
            },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("code_challenge=challenge-value", url);
        Assert.Contains("code_challenge_method=S256", url);
    }

    [Fact]
    public async Task GetAuthorizationUrlAsync_WhenSandboxEnvironment_UsesSandboxEndpoint()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK);
        var provider = CreateOAuthProvider(handler, DialpadEnvironment.Sandbox);

        // Act
        var url = await provider.GetAuthorizationUrlAsync(
            new TelephonyAuthorizationContext { RedirectUri = "https://site.test/cb", State = "xyz" },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.StartsWith("https://sandbox.dialpad.com/oauth2/authorize", url);
    }

    [Fact]
    public async Task GetAuthorizationUrlAsync_WhenHostConfigured_UsesConfiguredHost()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK);
        var provider = CreateOAuthProvider(handler, DialpadEnvironment.Sandbox, host: "dialpadbeta.com");

        // Act
        var url = await provider.GetAuthorizationUrlAsync(
            new TelephonyAuthorizationContext { RedirectUri = "https://site.test/cb", State = "xyz" },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.StartsWith("https://dialpadbeta.com/oauth2/authorize", url);
    }

    [Fact]
    public async Task ExchangeCodeAsync_PostsToTokenEndpoint_AndParsesTokens()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "{\"access_token\":\"at\",\"refresh_token\":\"rt\",\"expires_in\":3600,\"token_type\":\"Bearer\"}");
        var provider = CreateOAuthProvider(handler);

        // Act
        var tokens = await provider.ExchangeCodeAsync(
            new TelephonyCodeExchangeContext { Code = "auth-code", RedirectUri = "https://site.test/cb" },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(tokens);
        Assert.Equal("at", tokens.AccessToken);
        Assert.Equal("rt", tokens.RefreshToken);
        Assert.NotNull(tokens.ExpiresUtc);
        Assert.Equal("https://dialpad.com/oauth2/token", handler.LastRequest.RequestUri.AbsoluteUri);
        Assert.Contains("grant_type=authorization_code", handler.LastRequestBody);
        Assert.Contains("code=auth-code", handler.LastRequestBody);
        Assert.Contains("client_id=client-id", handler.LastRequestBody);
        Assert.Contains("client_secret=client-secret", handler.LastRequestBody);
    }

    [Fact]
    public async Task ExchangeCodeAsync_WhenCodeVerifierProvided_IncludesPkceVerifierInBody()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "{\"access_token\":\"at\",\"token_type\":\"Bearer\"}");
        var provider = CreateOAuthProvider(handler);

        // Act
        var tokens = await provider.ExchangeCodeAsync(
            new TelephonyCodeExchangeContext { Code = "auth-code", RedirectUri = "https://site.test/cb", CodeVerifier = "verifier-1" },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(tokens);
        Assert.Contains("code_verifier=verifier-1", handler.LastRequestBody);
    }

    [Fact]
    public async Task ExchangeCodeAsync_WhenSandboxEnvironment_PostsToSandboxTokenEndpoint()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "{\"access_token\":\"at\",\"token_type\":\"Bearer\"}");
        var provider = CreateOAuthProvider(handler, DialpadEnvironment.Sandbox);

        // Act
        await provider.ExchangeCodeAsync(
            new TelephonyCodeExchangeContext { Code = "auth-code", RedirectUri = "https://site.test/cb" },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("https://sandbox.dialpad.com/oauth2/token", handler.LastRequest.RequestUri.AbsoluteUri);
    }

    [Fact]
    public async Task ExchangeCodeAsync_WhenHostConfigured_PostsToConfiguredHostTokenEndpoint()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "{\"access_token\":\"at\",\"token_type\":\"Bearer\"}");
        var provider = CreateOAuthProvider(handler, DialpadEnvironment.Sandbox, host: "dialpadbeta.com");

        // Act
        await provider.ExchangeCodeAsync(
            new TelephonyCodeExchangeContext { Code = "auth-code", RedirectUri = "https://site.test/cb" },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("https://dialpadbeta.com/oauth2/token", handler.LastRequest.RequestUri.AbsoluteUri);
    }

    [Fact]
    public async Task RevokeTokensAsync_PostsToDeauthorizeEndpointWithBearerToken()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK);
        var provider = CreateOAuthProvider(handler);

        // Act
        var result = await provider.RevokeTokensAsync(
            new TelephonyUserTokens { AccessToken = "access-1" },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Post, handler.LastRequest.Method);
        Assert.Equal("https://dialpad.com/oauth2/deauthorize", handler.LastRequest.RequestUri.AbsoluteUri);
        Assert.Equal("Bearer", handler.LastRequest.Headers.Authorization.Scheme);
        Assert.Equal("access-1", handler.LastRequest.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task RevokeTokensAsync_WhenProviderRejectsRequest_ReturnsFailedResult()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(HttpStatusCode.BadRequest);
        var provider = CreateOAuthProvider(handler);

        // Act
        var result = await provider.RevokeTokensAsync(
            new TelephonyUserTokens { AccessToken = "access-1" },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.False(result.OutcomeUnknown);
        Assert.NotNull(handler.LastRequest);
    }

    [Fact]
    public async Task RevokeTokensAsync_WhenProviderReturnsAmbiguousStatus_ReturnsUnknown()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(HttpStatusCode.ServiceUnavailable);
        var provider = CreateOAuthProvider(handler);

        // Act
        var result = await provider.RevokeTokensAsync(
            new TelephonyUserTokens { AccessToken = "access-1" },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.True(result.OutcomeUnknown);
        Assert.NotNull(handler.LastRequest);
    }

    [Fact]
    public async Task RevokeTokensAsync_WhenAuthenticationSwitchedToApiKeyButTokenExists_StillCallsDeauthorize()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK);
        var provider = CreateOAuthProvider(handler, authenticationType: DialpadAuthenticationType.ApiKey);

        // Act
        var result = await provider.RevokeTokensAsync(
            new TelephonyUserTokens { AccessToken = "access-1" },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.NotNull(handler.LastRequest);
        Assert.Equal("https://dialpad.com/oauth2/deauthorize", handler.LastRequest.RequestUri.AbsoluteUri);
    }

    [Fact]
    public async Task RevokeTokensAsync_WhenNoAccessToken_DoesNotCallApi()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK);
        var provider = CreateOAuthProvider(handler);

        // Act
        var result = await provider.RevokeTokensAsync(
            new TelephonyUserTokens { AccessToken = "" },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public void RequiresUserAuthentication_WhenApiKeyAuthenticationSelected_ReturnsFalse()
    {
        // Arrange
        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var protectedSecret = dataProtectionProvider
            .CreateProtector(DialpadConstants.OAuthProtectorName)
            .Protect("client-secret");

        var settings = new DialpadSettings
        {
            IsEnabled = true,
            Production = new DialpadEnvironmentSettings
            {
                AuthenticationType = DialpadAuthenticationType.ApiKey,
                ClientId = "client-id",
                ClientSecret = protectedSecret,
            },
        };

        var provider = new DialpadTelephonyProvider(
            SiteServiceFactory.Create(settings),
            dataProtectionProvider,
            new StubHttpClientFactory(new StubHttpMessageHandler(HttpStatusCode.OK)),
            Mock.Of<ITelephonyAuthenticationService>(),
            new StubClock(),
            NullLogger<DialpadTelephonyProvider>.Instance,
            new PassThroughStringLocalizer<DialpadTelephonyProvider>());

        // Act
        var requiresUserAuthentication = provider.RequiresUserAuthentication;

        // Assert
        Assert.False(requiresUserAuthentication);
    }

    private static DialpadTelephonyProvider CreateOAuthProvider(StubHttpMessageHandler handler, DialpadEnvironment environment = DialpadEnvironment.Production, DialpadAuthenticationType authenticationType = DialpadAuthenticationType.OAuth2, string host = null, string scopes = "calls")
    {
        var dataProtectionProvider = new EphemeralDataProtectionProvider();

        var protectedSecret = dataProtectionProvider
            .CreateProtector(DialpadConstants.OAuthProtectorName)
            .Protect("client-secret");

        var environmentSettings = new DialpadEnvironmentSettings
        {
            AuthenticationType = authenticationType,
            ClientId = "client-id",
            ClientSecret = protectedSecret,
            Scopes = scopes,
            ApiBaseUrl = BaseUrl,
            Host = host,
        };

        var settings = new DialpadSettings
        {
            IsEnabled = true,
            Environment = environment,
        };

        if (environment == DialpadEnvironment.Sandbox)
        {
            settings.Sandbox = environmentSettings;
        }
        else
        {
            settings.Production = environmentSettings;
        }

        return new DialpadTelephonyProvider(
            SiteServiceFactory.Create(settings),
            dataProtectionProvider,
            new StubHttpClientFactory(handler),
            Mock.Of<ITelephonyAuthenticationService>(),
            new StubClock(),
            NullLogger<DialpadTelephonyProvider>.Instance,
            new PassThroughStringLocalizer<DialpadTelephonyProvider>());
    }
}
