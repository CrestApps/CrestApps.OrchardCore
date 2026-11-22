using CrestApps.OrchardCore.Asterisk.Web;
using CrestApps.OrchardCore.Asterisk.Web.Services;
using Microsoft.Extensions.Hosting;

namespace CrestApps.OrchardCore.Tests.Telephony;

public sealed class AsteriskWebSecurityTests
{
    [Fact]
    public void EnsureDevelopmentOnly_WhenProduction_Throws()
    {
        // Act
        var exception = Assert.Throws<InvalidOperationException>(
            () => AsteriskWebSecurity.EnsureDevelopmentOnly(Environments.Production));

        // Assert
        Assert.Contains("Development", exception.Message);
    }

    [Fact]
    public void EnsureDevelopmentOnly_WhenDevelopment_DoesNotThrow()
    {
        // Act
        var exception = Record.Exception(
            () => AsteriskWebSecurity.EnsureDevelopmentOnly(Environments.Development));

        // Assert
        Assert.Null(exception);
    }

    // SEC-P10d: the sample's call-control API answered anyone who could reach it; "Development only" was the only guard.
    [Theory]
    [InlineData("localhost:59496", "127.0.0.1", "/", false, true)]
    [InlineData("127.0.0.1:59496", "::1", "/Simulator", false, true)]
    [InlineData("[::1]:59496", "::1", "/", false, true)]
    [InlineData("evil.example", "127.0.0.1", "/", false, false)]
    [InlineData("localhost.evil.example", "127.0.0.1", "/", false, false)]
    [InlineData("localhost:59496", "10.0.0.5", "/", false, false)]
    [InlineData("localhost:59496", "127.0.0.1", "/api/asterisk/dashboard", false, false)]
    [InlineData("localhost:59496", "127.0.0.1", "/api/asterisk/channels/abc", true, true)]
    [InlineData("localhost:59496", "127.0.0.1", "/hubs/asterisk-dashboard", false, false)]
    [InlineData("localhost:59496", "127.0.0.1", "/hubs/asterisk-dashboard/negotiate", false, false)]
    [InlineData("localhost:59496", "127.0.0.1", "/hubs/asterisk-dashboard/negotiate", true, true)]
    public void IsAllowedRequest_OnlyThisMachine_AndTheApiOnlyWithTheKey(string host, string remoteAddress, string path, bool withKey, bool allowed)
    {
        // Arrange
        const string key = "sample-key-0123456789";
        var context = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        context.Request.Host = new Microsoft.AspNetCore.Http.HostString(host);
        context.Request.Path = path;
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse(remoteAddress);

        if (withKey)
        {
            context.Request.Headers[AsteriskWebSecurity.ApiKeyHeaderName] = key;
        }

        // Act
        var result = AsteriskWebSecurity.IsAllowedRequest(context, key);

        // Assert
        Assert.Equal(allowed, result);
    }

    [Fact]
    public void IsAllowedRequest_RefusesTheApi_WithAWrongKey()
    {
        var context = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        context.Request.Host = new Microsoft.AspNetCore.Http.HostString("localhost");
        context.Request.Path = "/api/asterisk/simulations/two-party";
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Loopback;
        context.Request.Headers[AsteriskWebSecurity.ApiKeyHeaderName] = "wrong";

        Assert.False(AsteriskWebSecurity.IsAllowedRequest(context, "sample-key-0123456789"));
    }

    /// <summary>
    /// Mild (review 11): the dashboard's SignalR hub streamed live caller numbers to any page open on the machine, since
    /// a browser opens a WebSocket to localhost from any site and the hub was outside the key check.
    /// </summary>
    [Theory]
    [InlineData(null, null, null, false)]
    [InlineData("key", null, null, true)]
    [InlineData("bearer", null, null, true)]
    [InlineData("query", null, null, true)]
    [InlineData("query", "https://localhost:59496", null, true)]
    [InlineData("query", "https://evil.example", null, false)]
    [InlineData("query", "http://localhost:59496", null, false)]
    [InlineData("query", "https://localhost:1234", null, false)]
    [InlineData("query", null, "wrong", false)]
    public void IsAllowedRequest_TheDashboardHub_NeedsTheKey_FromThisOrigin(string keyIn, string origin, string wrongKey, bool allowed)
    {
        // Arrange
        const string key = "sample-key-0123456789";
        var given = wrongKey ?? key;
        var context = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new Microsoft.AspNetCore.Http.HostString("localhost:59496");
        context.Request.Path = "/hubs/asterisk-dashboard";
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Loopback;

        switch (keyIn)
        {
            case "key":
                context.Request.Headers[AsteriskWebSecurity.ApiKeyHeaderName] = given;
                break;
            case "bearer":
                context.Request.Headers.Authorization = "Bearer " + given;
                break;
            case "query":
                context.Request.QueryString = Microsoft.AspNetCore.Http.QueryString.Create("access_token", given);
                break;
        }

        if (origin is not null)
        {
            context.Request.Headers.Origin = origin;
        }

        // Act
        var result = AsteriskWebSecurity.IsAllowedRequest(context, key);

        // Assert
        Assert.Equal(allowed, result);
    }

    [Fact]
    public void IsAllowedRequest_RefusesTheApi_FromAnotherOrigin_EvenWithTheKey()
    {
        var context = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new Microsoft.AspNetCore.Http.HostString("localhost:59496");
        context.Request.Path = "/api/asterisk/dashboard";
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Loopback;
        context.Request.Headers[AsteriskWebSecurity.ApiKeyHeaderName] = "sample-key-0123456789";
        context.Request.Headers.Origin = "https://evil.example";

        Assert.False(AsteriskWebSecurity.IsAllowedRequest(context, "sample-key-0123456789"));
    }

    [Fact]
    public void IsAllowedRequest_TheApiDoesNotTakeTheKeyFromTheQueryString()
    {
        var context = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        context.Request.Host = new Microsoft.AspNetCore.Http.HostString("localhost:59496");
        context.Request.Path = "/api/asterisk/dashboard";
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Loopback;
        context.Request.QueryString = Microsoft.AspNetCore.Http.QueryString.Create("access_token", "sample-key-0123456789");

        Assert.False(AsteriskWebSecurity.IsAllowedRequest(context, "sample-key-0123456789"));
    }

    [Fact]
    public void CreateEventsUriForLogging_DoesNotContainAriCredentialsOrApiKey()
    {
        // Arrange
        const string secret = "known-dashboard-secret";
        var options = new AsteriskWebOptions
        {
            AsteriskBaseUrl = "https://asterisk.test/ari/",
            AsteriskUserName = "dashboard-user",
            AsteriskPassword = secret,
            AsteriskApplicationName = "dashboard",
        };

        // Act
        var uri = AsteriskAriConnectionUtilities.CreateEventsUriForLogging(options);

        // Assert
        Assert.NotNull(uri);
        Assert.DoesNotContain(secret, uri.ToString());
        Assert.DoesNotContain("dashboard-user", uri.ToString());
        Assert.DoesNotContain("api_key", uri.Query, StringComparison.OrdinalIgnoreCase);
    }
}
