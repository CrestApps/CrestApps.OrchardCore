using System.Security.Claims;
using CrestApps.OrchardCore.AI.A2A.Handlers;
using CrestApps.OrchardCore.AI.A2A.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Tests.A2A;

public sealed class A2AApiKeyAuthenticationHandlerTests
{
    private const string ValidApiKey = "test-api-key-12345";

    // ───────────────────────────────────────────────────────────────
    // Non-ApiKey authentication type — handler should not participate
    // ───────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(A2AHostAuthenticationType.None)]
    [InlineData(A2AHostAuthenticationType.OpenId)]
    public async Task NonApiKeyMode_ReturnsNoResult(A2AHostAuthenticationType authType)
    {
        var result = await AuthenticateAsync(
            new A2AHostOptions { AuthenticationType = authType, ApiKey = ValidApiKey },
            $"Bearer {ValidApiKey}");

        Assert.False(result.Succeeded);
        Assert.True(result.None);
    }

    // ───────────────────────────────────────────────────────────────
    // ApiKey mode — server API key not configured
    // ───────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task ApiKeyMode_ServerKeyNotConfigured_ReturnsFail(string serverApiKey)
    {
        var result = await AuthenticateAsync(
            new A2AHostOptions { AuthenticationType = A2AHostAuthenticationType.ApiKey, ApiKey = serverApiKey },
            $"Bearer some-key");

        Assert.False(result.Succeeded);
        Assert.False(result.None);
        Assert.Contains("not configured", result.Failure.Message);
    }

    // ───────────────────────────────────────────────────────────────
    // ApiKey mode — no Authorization header
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task ApiKeyMode_NoAuthorizationHeader_ReturnsNoResult()
    {
        var result = await AuthenticateAsync(
            new A2AHostOptions { AuthenticationType = A2AHostAuthenticationType.ApiKey, ApiKey = ValidApiKey },
            authorizationHeader: null);

        Assert.False(result.Succeeded);
        Assert.True(result.None);
    }

    // ───────────────────────────────────────────────────────────────
    // ApiKey mode — empty Authorization header
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task ApiKeyMode_EmptyAuthorizationHeader_ReturnsNoResult()
    {
        var result = await AuthenticateAsync(
            new A2AHostOptions { AuthenticationType = A2AHostAuthenticationType.ApiKey, ApiKey = ValidApiKey },
            authorizationHeader: "");

        Assert.False(result.Succeeded);
        Assert.True(result.None);
    }

    // ───────────────────────────────────────────────────────────────
    // ApiKey mode — successful authentication with various prefixes
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task ApiKeyMode_BearerPrefix_CorrectKey_Succeeds()
    {
        var result = await AuthenticateAsync(
            new A2AHostOptions { AuthenticationType = A2AHostAuthenticationType.ApiKey, ApiKey = ValidApiKey },
            $"Bearer {ValidApiKey}");

        Assert.True(result.Succeeded);
        AssertValidTicket(result);
    }

    [Fact]
    public async Task ApiKeyMode_ApiKeyPrefix_CorrectKey_Succeeds()
    {
        var result = await AuthenticateAsync(
            new A2AHostOptions { AuthenticationType = A2AHostAuthenticationType.ApiKey, ApiKey = ValidApiKey },
            $"ApiKey {ValidApiKey}");

        Assert.True(result.Succeeded);
        AssertValidTicket(result);
    }

    [Fact]
    public async Task ApiKeyMode_RawKey_CorrectKey_Succeeds()
    {
        var result = await AuthenticateAsync(
            new A2AHostOptions { AuthenticationType = A2AHostAuthenticationType.ApiKey, ApiKey = ValidApiKey },
            ValidApiKey);

        Assert.True(result.Succeeded);
        AssertValidTicket(result);
    }

    [Fact]
    public async Task ApiKeyMode_BearerPrefixCaseInsensitive_CorrectKey_Succeeds()
    {
        var result = await AuthenticateAsync(
            new A2AHostOptions { AuthenticationType = A2AHostAuthenticationType.ApiKey, ApiKey = ValidApiKey },
            $"bearer {ValidApiKey}");

        Assert.True(result.Succeeded);
        AssertValidTicket(result);
    }

    [Fact]
    public async Task ApiKeyMode_ApiKeyPrefixCaseInsensitive_CorrectKey_Succeeds()
    {
        var result = await AuthenticateAsync(
            new A2AHostOptions { AuthenticationType = A2AHostAuthenticationType.ApiKey, ApiKey = ValidApiKey },
            $"apikey {ValidApiKey}");

        Assert.True(result.Succeeded);
        AssertValidTicket(result);
    }

    // ───────────────────────────────────────────────────────────────
    // ApiKey mode — wrong key
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task ApiKeyMode_WrongKey_ReturnsFail()
    {
        var result = await AuthenticateAsync(
            new A2AHostOptions { AuthenticationType = A2AHostAuthenticationType.ApiKey, ApiKey = ValidApiKey },
            "Bearer wrong-key");

        Assert.False(result.Succeeded);
        Assert.False(result.None);
        Assert.Contains("Invalid", result.Failure.Message);
    }

    [Fact]
    public async Task ApiKeyMode_SimilarButDifferentKey_ReturnsFail()
    {
        var result = await AuthenticateAsync(
            new A2AHostOptions { AuthenticationType = A2AHostAuthenticationType.ApiKey, ApiKey = ValidApiKey },
            $"Bearer {ValidApiKey}x");

        Assert.False(result.Succeeded);
        Assert.False(result.None);
    }

    // ───────────────────────────────────────────────────────────────
    // ApiKey mode — empty key after prefix
    // ───────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Bearer ")]
    [InlineData("ApiKey ")]
    [InlineData("Bearer  ")]
    public async Task ApiKeyMode_EmptyKeyAfterPrefix_ReturnsNoResult(string header)
    {
        var result = await AuthenticateAsync(
            new A2AHostOptions { AuthenticationType = A2AHostAuthenticationType.ApiKey, ApiKey = ValidApiKey },
            header);

        Assert.False(result.Succeeded);
        Assert.True(result.None);
    }

    // ───────────────────────────────────────────────────────────────
    // ApiKey mode — key comparison is case-sensitive
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task ApiKeyMode_KeyIsCaseSensitive_ReturnsFail()
    {
        var result = await AuthenticateAsync(
            new A2AHostOptions { AuthenticationType = A2AHostAuthenticationType.ApiKey, ApiKey = "AbCdEf" },
            "Bearer abcdef");

        Assert.False(result.Succeeded);
        Assert.False(result.None);
    }

    // ───────────────────────────────────────────────────────────────
    // Successful ticket claims validation
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task ApiKeyMode_SuccessfulAuth_HasCorrectAuthenticationScheme()
    {
        var result = await AuthenticateAsync(
            new A2AHostOptions { AuthenticationType = A2AHostAuthenticationType.ApiKey, ApiKey = ValidApiKey },
            $"Bearer {ValidApiKey}");

        Assert.True(result.Succeeded);
        Assert.Equal(A2AApiKeyAuthenticationDefaults.AuthenticationScheme, result.Ticket.AuthenticationScheme);
        Assert.Equal(A2AApiKeyAuthenticationDefaults.AuthenticationScheme, result.Principal.Identity.AuthenticationType);
    }

    [Fact]
    public async Task ApiKeyMode_SuccessfulAuth_HasNameIdentifierClaim()
    {
        var result = await AuthenticateAsync(
            new A2AHostOptions { AuthenticationType = A2AHostAuthenticationType.ApiKey, ApiKey = ValidApiKey },
            $"Bearer {ValidApiKey}");

        Assert.True(result.Succeeded);

        var nameIdClaim = result.Principal.FindFirst(ClaimTypes.NameIdentifier);
        Assert.NotNull(nameIdClaim);
        Assert.NotEmpty(nameIdClaim.Value);
    }

    // ───────────────────────────────────────────────────────────────
    // A2A uses constant-time comparison (FixedTimeEquals)
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task ApiKeyMode_KeyWithExtraWhitespace_TrimsAndSucceeds()
    {
        var result = await AuthenticateAsync(
            new A2AHostOptions { AuthenticationType = A2AHostAuthenticationType.ApiKey, ApiKey = ValidApiKey },
            $"Bearer  {ValidApiKey} ");

        Assert.True(result.Succeeded);
    }

    // ───────────────────────────────────────────────────────────────
    // Helpers
    // ───────────────────────────────────────────────────────────────

    private static void AssertValidTicket(AuthenticateResult result)
    {
        Assert.NotNull(result.Ticket);
        Assert.NotNull(result.Principal);
        Assert.True(result.Principal.Identity.IsAuthenticated);
        Assert.Equal(A2AApiKeyAuthenticationDefaults.AuthenticationScheme, result.Ticket.AuthenticationScheme);
    }

    private static async Task<AuthenticateResult> AuthenticateAsync(
        A2AHostOptions hostOptions,
        string authorizationHeader)
    {
        var httpContext = new DefaultHttpContext();

        if (authorizationHeader != null)
        {
            httpContext.Request.Headers["Authorization"] = authorizationHeader;
        }

        var scheme = new AuthenticationScheme(
            A2AApiKeyAuthenticationDefaults.AuthenticationScheme,
            displayName: null,
            handlerType: typeof(A2AApiKeyAuthenticationHandler));

        var authOptionsMonitor = CreateOptionsMonitor(new A2AApiKeyAuthenticationOptions());
        var hostOptionsMonitor = CreateOptionsMonitor(hostOptions);

        var handler = new A2AApiKeyAuthenticationHandler(
            authOptionsMonitor,
            hostOptionsMonitor,
            NullLoggerFactory.Instance,
            System.Text.Encodings.Web.UrlEncoder.Default);

        await handler.InitializeAsync(scheme, httpContext);

        return await handler.AuthenticateAsync();
    }

    private static TestOptionsMonitor<T> CreateOptionsMonitor<T>(T value)
        where T : class, new()
    {
        return new TestOptionsMonitor<T>(value);
    }

    private sealed class TestOptionsMonitor<T> : IOptionsMonitor<T>
    {
        public TestOptionsMonitor(T value) => CurrentValue = value;

        public T CurrentValue { get; }

        public T Get(string name) => CurrentValue;

        public IDisposable OnChange(Action<T, string> listener) => null;
    }
}
