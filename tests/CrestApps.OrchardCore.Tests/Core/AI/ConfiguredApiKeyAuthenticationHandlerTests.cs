using System.Text.Encodings.Web;
using CrestApps.Core.AI.Mcp.Models;
using CrestApps.OrchardCore.AI.Core.ApiKeys;
using CrestApps.OrchardCore.AI.Mcp.Handlers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Tests.Core.AI;

/// <summary>
/// The A2A and MCP API-key handlers were 90% identical copies, each with a private constant-time comparison. They now
/// share one base; the A2A handler keeps its own behavioral tests, and these pin the shared parts and the MCP handler.
/// </summary>
public sealed class ConfiguredApiKeyAuthenticationHandlerTests
{
    private const string ValidApiKey = "test-api-key-12345";

    [Theory]
    [InlineData("Bearer abc", "abc")]
    [InlineData("bearer  abc ", "abc")]
    [InlineData("ApiKey abc", "abc")]
    [InlineData("abc", "abc")]
    [InlineData("Bearer ", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void ReadApiKey_ReadsEveryAcceptedForm(string header, string expected)
        => Assert.Equal(expected, ConfiguredApiKeyAuthenticationHandler<AuthenticationSchemeOptions>.ReadApiKey(header));

    [Theory]
    [InlineData("abc", "abc", true)]
    [InlineData("abc", "abd", false)]
    [InlineData("abc", "abcd", false)]
    [InlineData(null, "abc", false)]
    public void KeysMatch_ComparesTheWholeKey(string presented, string configured, bool expected)
        => Assert.Equal(expected, ConfiguredApiKeyAuthenticationHandler<AuthenticationSchemeOptions>.KeysMatch(presented, configured));

    [Fact]
    public async Task Mcp_WithTheConfiguredKey_SignsTheCallerIn()
    {
        var result = await AuthenticateAsync(McpServerAuthenticationType.ApiKey, ValidApiKey, $"Bearer {ValidApiKey}");

        Assert.True(result.Succeeded);
        Assert.Equal(McpApiKeyAuthenticationDefaults.AuthenticationScheme, result.Ticket.AuthenticationScheme);
        Assert.Equal("mcp-api-key-user", result.Principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value);
    }

    [Fact]
    public async Task Mcp_WithAWrongKey_Fails()
    {
        var result = await AuthenticateAsync(McpServerAuthenticationType.ApiKey, ValidApiKey, "Bearer wrong");

        Assert.False(result.Succeeded);
        Assert.Contains("Invalid API key", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Mcp_WhenApiKeyAuthenticationIsOff_DoesNotTakePart()
    {
        var result = await AuthenticateAsync(McpServerAuthenticationType.None, ValidApiKey, $"Bearer {ValidApiKey}");

        Assert.True(result.None);
    }

    private static async Task<AuthenticateResult> AuthenticateAsync(McpServerAuthenticationType authenticationType, string serverApiKey, string authorizationHeader)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Authorization"] = authorizationHeader;

        var handler = new McpApiKeyAuthenticationHandler(
            new TestOptionsMonitor<McpApiKeyAuthenticationOptions>(new McpApiKeyAuthenticationOptions()),
            new TestOptionsMonitor<McpServerOptions>(new McpServerOptions { AuthenticationType = authenticationType, ApiKey = serverApiKey }),
            NullLoggerFactory.Instance,
            UrlEncoder.Default);

        await handler.InitializeAsync(
            new AuthenticationScheme(McpApiKeyAuthenticationDefaults.AuthenticationScheme, displayName: null, handlerType: typeof(McpApiKeyAuthenticationHandler)),
            httpContext);

        return await handler.AuthenticateAsync();
    }

    private sealed class TestOptionsMonitor<T> : IOptionsMonitor<T>
    {
        public TestOptionsMonitor(T value) => CurrentValue = value;

        public T CurrentValue { get; }

        public T Get(string name) => CurrentValue;

        public IDisposable OnChange(Action<T, string> listener) => null;
    }
}
