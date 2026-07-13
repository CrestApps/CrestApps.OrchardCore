using System.Net;
using CrestApps.OrchardCore.DialPad;
using CrestApps.OrchardCore.DialPad.Models;
using CrestApps.OrchardCore.DialPad.Services;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CrestApps.OrchardCore.Tests.Telephony;

public sealed class DialPadTelephonyProviderTests
{
    private const string PlainToken = "secret-token";
    private const string BaseUrl = "https://example.test/api/";

    [Fact]
    public async Task DialAsync_WhenConfigured_PostsToDialPadApiWithBearerToken()
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
        var settings = new DialPadSettings
        {
            IsEnabled = true,
            AuthenticationType = DialPadAuthenticationType.NotConfigured,
            ApiBaseUrl = BaseUrl,
        };

        var provider = new DialPadTelephonyProvider(
            SiteServiceFactory.Create(settings),
            new EphemeralDataProtectionProvider(),
            new StubHttpClientFactory(handler),
            Mock.Of<ITelephonyAuthenticationService>(),
            new StubClock(),
            NullLogger<DialPadTelephonyProvider>.Instance,
            new PassThroughStringLocalizer<DialPadTelephonyProvider>());

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
    public async Task GetClientCredentialsAsync_WhenConfigured_ReturnsProviderName()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK);
        var provider = CreateProvider(handler, out _, isEnabled: true);

        // Act
        var credentials = await provider.GetClientCredentialsAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(credentials);
        Assert.Equal(DialPadConstants.ProviderTechnicalName, credentials.ProviderName);
        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task GetDirectoryAsync_WhenConfigured_MapsDialPadUsers()
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

    private static DialPadTelephonyProvider CreateProvider(StubHttpMessageHandler handler, out IDataProtectionProvider dataProtectionProvider, bool isEnabled)
    {
        dataProtectionProvider = new EphemeralDataProtectionProvider();

        var protectedToken = dataProtectionProvider
            .CreateProtector(DialPadConstants.ProtectorName)
            .Protect(PlainToken);

        var settings = new DialPadSettings
        {
            IsEnabled = isEnabled,
            ApiToken = protectedToken,
            ApiBaseUrl = BaseUrl,
            OutboundCallerId = "+15550000000",
            UserId = "user-1",
        };

        return new DialPadTelephonyProvider(
            SiteServiceFactory.Create(settings),
            dataProtectionProvider,
            new StubHttpClientFactory(handler),
            Mock.Of<ITelephonyAuthenticationService>(),
            new StubClock(),
            NullLogger<DialPadTelephonyProvider>.Instance,
            new PassThroughStringLocalizer<DialPadTelephonyProvider>());
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
        Assert.Contains("offline_access", url);
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
        var provider = CreateOAuthProvider(handler, DialPadEnvironment.Sandbox);

        // Act
        var url = await provider.GetAuthorizationUrlAsync(
            new TelephonyAuthorizationContext { RedirectUri = "https://site.test/cb", State = "xyz" },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.StartsWith("https://sandbox.dialpad.com/oauth2/authorize", url);
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
        var provider = CreateOAuthProvider(handler, DialPadEnvironment.Sandbox);

        // Act
        await provider.ExchangeCodeAsync(
            new TelephonyCodeExchangeContext { Code = "auth-code", RedirectUri = "https://site.test/cb" },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("https://sandbox.dialpad.com/oauth2/token", handler.LastRequest.RequestUri.AbsoluteUri);
    }

    [Fact]
    public async Task RevokeTokensAsync_PostsToDeauthorizeEndpointWithBearerToken()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK);
        var provider = CreateOAuthProvider(handler);

        // Act
        await provider.RevokeTokensAsync(
            new TelephonyUserTokens { AccessToken = "access-1" },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Post, handler.LastRequest.Method);
        Assert.Equal("https://dialpad.com/oauth2/deauthorize", handler.LastRequest.RequestUri.AbsoluteUri);
        Assert.Equal("Bearer", handler.LastRequest.Headers.Authorization.Scheme);
        Assert.Equal("access-1", handler.LastRequest.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task RevokeTokensAsync_WhenNoAccessToken_DoesNotCallApi()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK);
        var provider = CreateOAuthProvider(handler);

        // Act
        await provider.RevokeTokensAsync(
            new TelephonyUserTokens { AccessToken = "" },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public void RequiresUserAuthentication_WhenApiKeyAuthenticationSelected_ReturnsFalse()
    {
        // Arrange
        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var protectedSecret = dataProtectionProvider
            .CreateProtector(DialPadConstants.OAuthProtectorName)
            .Protect("client-secret");

        var settings = new DialPadSettings
        {
            IsEnabled = true,
            AuthenticationType = DialPadAuthenticationType.ApiKey,
            ClientId = "client-id",
            ClientSecret = protectedSecret,
        };

        var provider = new DialPadTelephonyProvider(
            SiteServiceFactory.Create(settings),
            dataProtectionProvider,
            new StubHttpClientFactory(new StubHttpMessageHandler(HttpStatusCode.OK)),
            Mock.Of<ITelephonyAuthenticationService>(),
            new StubClock(),
            NullLogger<DialPadTelephonyProvider>.Instance,
            new PassThroughStringLocalizer<DialPadTelephonyProvider>());

        // Act
        var requiresUserAuthentication = provider.RequiresUserAuthentication;

        // Assert
        Assert.False(requiresUserAuthentication);
    }

    [Fact]
    public void UseOAuth_Setter_PreservesLegacySettingsCompatibility()
    {
        // Arrange
        var settings = new DialPadSettings();

        // Act
        settings.UseOAuth = true;

        // Assert
        Assert.Equal(DialPadAuthenticationType.OAuth2, settings.AuthenticationType);
        Assert.True(settings.UseOAuth);
    }

    private static DialPadTelephonyProvider CreateOAuthProvider(StubHttpMessageHandler handler, DialPadEnvironment environment = DialPadEnvironment.Production)
    {
        var dataProtectionProvider = new EphemeralDataProtectionProvider();

        var protectedSecret = dataProtectionProvider
            .CreateProtector(DialPadConstants.OAuthProtectorName)
            .Protect("client-secret");

        var settings = new DialPadSettings
        {
            IsEnabled = true,
            Environment = environment,
            AuthenticationType = DialPadAuthenticationType.OAuth2,
            ClientId = "client-id",
            ClientSecret = protectedSecret,
            Scopes = "calls",
            ApiBaseUrl = BaseUrl,
        };

        return new DialPadTelephonyProvider(
            SiteServiceFactory.Create(settings),
            dataProtectionProvider,
            new StubHttpClientFactory(handler),
            Mock.Of<ITelephonyAuthenticationService>(),
            new StubClock(),
            NullLogger<DialPadTelephonyProvider>.Instance,
            new PassThroughStringLocalizer<DialPadTelephonyProvider>());
    }
}
