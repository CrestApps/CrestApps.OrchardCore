using System.Net;
using System.Net.Http;
using System.Text;
using CrestApps.OrchardCore.DataConnectors;
using CrestApps.OrchardCore.DataConnectors.Http;
using CrestApps.OrchardCore.DataConnectors.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Tests.DataConnectors;

public sealed class DataConnectorHttpTests
{
    private const string TokenResponse = """{ "access_token": "token-1", "token_type": "Bearer", "expires_in": 3600 }""";

    [Fact]
    public async Task ApplyAsync_WithAnApiKeyInAHeader_SendsItWithItsPrefix()
    {
        // Arrange
        var (authenticator, _) = CreateAuthenticator(_ => StubHttpHandler.Json("{}"));
        var settings = new HttpAuthenticationSettings { Type = HttpAuthenticationType.ApiKey, ApiKeyName = "Authorization", ApiKeyPrefix = "ApiKey " };
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/items");

        // Act
        await authenticator.ApplyAsync(request, settings, Secrets((HttpAuthenticationSecrets.ApiKey, "abc")), "key", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("ApiKey abc", string.Join(",", request.Headers.GetValues("Authorization")));
    }

    [Fact]
    public async Task ApplyAsync_WithAnApiKeyInTheQuery_AppendsItToTheQueryString()
    {
        // Arrange
        var (authenticator, _) = CreateAuthenticator(_ => StubHttpHandler.Json("{}"));
        var settings = new HttpAuthenticationSettings { Type = HttpAuthenticationType.ApiKey, ApiKeyName = "api_key", ApiKeyLocation = HttpApiKeyLocation.Query };
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/items?page=2");

        // Act
        await authenticator.ApplyAsync(request, settings, Secrets((HttpAuthenticationSecrets.ApiKey, "a b")), "key", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("?page=2&api_key=a%20b", request.RequestUri.Query);
    }

    [Fact]
    public async Task ApplyAsync_WithBasicCredentials_SendsThemEncoded()
    {
        // Arrange
        var (authenticator, _) = CreateAuthenticator(_ => StubHttpHandler.Json("{}"));
        var settings = new HttpAuthenticationSettings { Type = HttpAuthenticationType.Basic, UserName = "reader" };
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/");

        // Act
        await authenticator.ApplyAsync(request, settings, Secrets((HttpAuthenticationSecrets.Password, "secret")), "key", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("Basic", request.Headers.Authorization.Scheme);
        Assert.Equal("reader:secret", Encoding.UTF8.GetString(Convert.FromBase64String(request.Headers.Authorization.Parameter)));
    }

    [Fact]
    public async Task ApplyAsync_WithAMissingApiKey_FailsWithAMessageThatNamesNoSecret()
    {
        // Arrange
        var (authenticator, _) = CreateAuthenticator(_ => StubHttpHandler.Json("{}"));
        var settings = new HttpAuthenticationSettings { Type = HttpAuthenticationType.ApiKey };
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DataConnectorException>(() =>
            authenticator.ApplyAsync(request, settings, new FakeSecrets(), "key", TestContext.Current.CancellationToken));

        Assert.Contains("API key", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ApplyAsync_WithOAuthClientCredentials_AsksForATokenOnce_AndCachesIt()
    {
        // Arrange
        var (authenticator, handler) = CreateAuthenticator(_ => StubHttpHandler.Json(TokenResponse));
        var settings = new HttpAuthenticationSettings
        {
            Type = HttpAuthenticationType.OAuth2,
            TokenUrl = "https://login.example.com/token",
            ClientId = "client id",
            Scope = "api.read",
            Audience = "https://api.example.com",
        };
        var secrets = Secrets((HttpAuthenticationSecrets.ClientSecret, "s3cret"));

        using var first = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/a");
        using var second = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/b");

        // Act
        await authenticator.ApplyAsync(first, settings, secrets, "connection", TestContext.Current.CancellationToken);
        await authenticator.ApplyAsync(second, settings, secrets, "connection", TestContext.Current.CancellationToken);

        // Assert
        var tokenRequest = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, tokenRequest.Method);
        Assert.Contains("grant_type=client_credentials", tokenRequest.Body, StringComparison.Ordinal);
        Assert.Contains("scope=api.read", tokenRequest.Body, StringComparison.Ordinal);
        Assert.Contains("audience=", tokenRequest.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("client_secret", tokenRequest.Body, StringComparison.Ordinal);
        Assert.Equal("client%20id:s3cret", Encoding.UTF8.GetString(Convert.FromBase64String(tokenRequest.Authorization["Basic ".Length..])));
        Assert.Equal("Bearer token-1", first.Headers.Authorization.ToString());
        Assert.Equal("Bearer token-1", second.Headers.Authorization.ToString());
    }

    [Fact]
    public async Task ApplyAsync_WithOAuthInTheBody_SendsTheClientInTheForm()
    {
        // Arrange
        var (authenticator, handler) = CreateAuthenticator(_ => StubHttpHandler.Json(TokenResponse));
        var settings = new HttpAuthenticationSettings
        {
            Type = HttpAuthenticationType.OAuth2,
            TokenUrl = "https://login.example.com/token",
            ClientId = "app",
            ClientAuthentication = OAuthClientAuthentication.RequestBody,
            GrantType = OAuthGrantType.Password,
            UserName = "reader",
        };
        var secrets = Secrets((HttpAuthenticationSecrets.ClientSecret, "s3cret"), (HttpAuthenticationSecrets.Password, "pw"));
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/a");

        // Act
        await authenticator.ApplyAsync(request, settings, secrets, "connection", TestContext.Current.CancellationToken);

        // Assert
        var body = Assert.Single(handler.Requests).Body;
        Assert.Contains("grant_type=password", body, StringComparison.Ordinal);
        Assert.Contains("username=reader", body, StringComparison.Ordinal);
        Assert.Contains("password=pw", body, StringComparison.Ordinal);
        Assert.Contains("client_id=app", body, StringComparison.Ordinal);
        Assert.Contains("client_secret=s3cret", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ApplyAsync_WithOpenIdConnect_ReadsTheTokenEndpointFromTheDiscoveryDocument()
    {
        // Arrange
        var (authenticator, handler) = CreateAuthenticator(request => request.RequestUri.AbsolutePath.EndsWith("/.well-known/openid-configuration", StringComparison.Ordinal)
            ? StubHttpHandler.Json("""{ "issuer": "https://id.example.com/tenant", "token_endpoint": "https://id.example.com/tenant/connect/token" }""")
            : StubHttpHandler.Json(TokenResponse));
        var settings = new HttpAuthenticationSettings
        {
            Type = HttpAuthenticationType.OpenIdConnect,
            Authority = "https://id.example.com/tenant/",
            ClientId = "app",
        };
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/a");

        // Act
        await authenticator.ApplyAsync(request, settings, Secrets((HttpAuthenticationSecrets.ClientSecret, "s")), "connection", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal("https://id.example.com/tenant/.well-known/openid-configuration", handler.Requests[0].Uri.AbsoluteUri);
        Assert.Equal("https://id.example.com/tenant/connect/token", handler.Requests[1].Uri.AbsoluteUri);
        Assert.Equal("Bearer token-1", request.Headers.Authorization.ToString());
    }

    [Fact]
    public async Task ApplyAsync_WithATokenEndpointOverHttp_RefusesToSendTheSecret()
    {
        // Arrange
        var (authenticator, handler) = CreateAuthenticator(_ => StubHttpHandler.Json(TokenResponse));
        var settings = new HttpAuthenticationSettings { Type = HttpAuthenticationType.OAuth2, TokenUrl = "http://login.example.com/token", ClientId = "app" };
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/a");

        // Act & Assert
        await Assert.ThrowsAsync<DataConnectorException>(() =>
            authenticator.ApplyAsync(request, settings, Secrets((HttpAuthenticationSecrets.ClientSecret, "s")), "connection", TestContext.Current.CancellationToken));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task ApplyAsync_WhenTheTokenEndpointRefuses_ReportsItsErrorCode()
    {
        // Arrange
        var (authenticator, _) = CreateAuthenticator(_ => StubHttpHandler.Json("""{ "error": "invalid_client" }""", HttpStatusCode.BadRequest));
        var settings = new HttpAuthenticationSettings { Type = HttpAuthenticationType.OAuth2, TokenUrl = "https://login.example.com/token", ClientId = "app" };
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/a");

        // Act
        var exception = await Assert.ThrowsAsync<DataConnectorException>(() =>
            authenticator.ApplyAsync(request, settings, Secrets((HttpAuthenticationSecrets.ClientSecret, "do-not-show")), "connection", TestContext.Current.CancellationToken));

        // Assert
        Assert.Contains("invalid_client", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("do-not-show", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAsync_RetriesAThrottledRequest_ThenReturnsTheResponse()
    {
        // Arrange
        var calls = 0;
        var client = CreateClient(_ => ++calls < 3 ? new HttpResponseMessage(HttpStatusCode.TooManyRequests) : StubHttpHandler.Json("[]"), out _);

        // Act
        using var response = await client.SendAsync(Settings(), new FakeSecrets(), "key", HttpMethod.Get, new Uri("https://api.example.com/items"), null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, calls);
    }

    [Fact]
    public async Task SendAsync_FollowsARedirectOnTheSameServer()
    {
        // Arrange
        var client = CreateClient(request => request.RequestUri.AbsolutePath == "/old"
            ? new HttpResponseMessage(HttpStatusCode.Found) { Headers = { Location = new Uri("/new", UriKind.Relative) } }
            : StubHttpHandler.Json("[]"), out var handler);

        // Act
        using var response = await client.SendAsync(Settings(), new FakeSecrets(), "key", HttpMethod.Get, new Uri("https://api.example.com/old"), null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("/new", handler.Requests[^1].Uri.AbsolutePath);
    }

    [Fact]
    public async Task SendAsync_RefusesARedirectToAnotherServer_SoCredentialsNeverLeave()
    {
        // Arrange
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.Redirect) { Headers = { Location = new Uri("https://evil.example.net/steal") } }, out var handler);

        // Act
        var exception = await Assert.ThrowsAsync<DataConnectorException>(() =>
            client.SendAsync(Settings(), new FakeSecrets(), "key", HttpMethod.Get, new Uri("https://api.example.com/items"), null, TestContext.Current.CancellationToken));

        // Assert
        Assert.Contains("another server", exception.Message, StringComparison.Ordinal);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task SendAsync_WhenATokenIsRefused_AsksForANewOneOnce()
    {
        // Arrange
        var tokens = 0;
        var apiCalls = 0;
        var client = CreateClient(request =>
        {
            if (request.RequestUri.Host == "login.example.com")
            {
                tokens++;

                return StubHttpHandler.Json($$"""{ "access_token": "token-{{tokens}}", "expires_in": 3600 }""");
            }

            return ++apiCalls == 1 ? new HttpResponseMessage(HttpStatusCode.Unauthorized) : StubHttpHandler.Json("[]");
        }, out var handler);

        var settings = Settings();
        settings.Authentication = new HttpAuthenticationSettings { Type = HttpAuthenticationType.OAuth2, TokenUrl = "https://login.example.com/token", ClientId = "app" };

        // Act
        using var response = await client.SendAsync(settings, Secrets((HttpAuthenticationSecrets.ClientSecret, "s")), "key", HttpMethod.Get, new Uri("https://api.example.com/items"), null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, tokens);
        Assert.Equal("Bearer token-2", handler.Requests[^1].Authorization);
    }

    [Fact]
    public async Task SendAsync_WhenTheServerFails_ReportsTheUrlWithoutItsQueryString()
    {
        // Arrange
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("missing") }, out _);

        // Act
        var exception = await Assert.ThrowsAsync<DataConnectorException>(() =>
            client.SendAsync(Settings(), new FakeSecrets(), "key", HttpMethod.Get, new Uri("https://api.example.com/items?api_key=do-not-show"), null, TestContext.Current.CancellationToken));

        // Assert
        Assert.Contains("https://api.example.com/items answered 404", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("do-not-show", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_AppendsAPathToTheBaseUrl_AndRefusesAnotherServer()
    {
        // Arrange
        var settings = new HttpConnectionSettings { BaseUrl = "https://api.example.com/v2" };

        // Act
        var path = DataConnectorHttpClient.Resolve(settings, "/customers?active=true");
        var sameOrigin = DataConnectorHttpClient.Resolve(settings, "https://api.example.com/v2/next?page=2");

        // Assert
        Assert.Equal("https://api.example.com/v2/customers?active=true", path.AbsoluteUri);
        Assert.Equal("https://api.example.com/v2/next?page=2", sameOrigin.AbsoluteUri);
        Assert.Throws<DataConnectorException>(() => DataConnectorHttpClient.Resolve(settings, "https://other.example.com/x"));
        Assert.Throws<DataConnectorException>(() => DataConnectorHttpClient.GetBaseUri(new HttpConnectionSettings { BaseUrl = "https://user:pass@api.example.com" }));
        Assert.Throws<DataConnectorException>(() => DataConnectorHttpClient.GetBaseUri(new HttpConnectionSettings { BaseUrl = "ftp://api.example.com" }));
    }

    [Fact]
    public void CacheKey_ChangesWhenTheConnectionIsSaved()
    {
        // Arrange
        var connection = new DataConnection { ItemId = "c1", CreatedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) };
        var before = DataConnectorHttpClient.CacheKey(connection);

        // Act
        connection.ModifiedUtc = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);

        // Assert
        Assert.NotEqual(before, DataConnectorHttpClient.CacheKey(connection));
    }

    internal static (HttpRequestAuthenticator Authenticator, StubHttpHandler Handler) CreateAuthenticator(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var handler = new StubHttpHandler((request, _) => respond(request));
        var authenticator = new HttpRequestAuthenticator(new StubHttpClientFactory(handler), new MemoryCache(new MemoryCacheOptions()), new ShellSettings { Name = "Default" });

        return (authenticator, handler);
    }

    internal static DataConnectorHttpClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> respond, out StubHttpHandler handler)
    {
        handler = new StubHttpHandler((request, _) => respond(request));
        var factory = new StubHttpClientFactory(handler);
        var authenticator = new HttpRequestAuthenticator(factory, new MemoryCache(new MemoryCacheOptions()), new ShellSettings { Name = "Default" });

        return new DataConnectorHttpClient(factory, authenticator, Options.Create(new DataConnectorNetworkOptions()))
        {
            Delay = (_, _) => Task.CompletedTask,
        };
    }

    internal static FakeSecrets Secrets(params (string Name, string Value)[] values)
        => new(values.ToDictionary(value => value.Name, value => value.Value, StringComparer.OrdinalIgnoreCase));

    private static HttpConnectionSettings Settings()
        => new() { BaseUrl = "https://api.example.com" };
}
