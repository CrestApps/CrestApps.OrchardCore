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
