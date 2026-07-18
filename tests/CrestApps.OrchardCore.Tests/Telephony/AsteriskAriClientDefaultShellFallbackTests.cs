using System.Net;
using CrestApps.OrchardCore.Asterisk.Models;
using CrestApps.OrchardCore.Asterisk.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Tests.Telephony;

public sealed class AsteriskAriClientDefaultShellFallbackTests
{
    private const string BaseUrl = "http://asterisk.example/ari/";

    [Fact]
    public async Task ResolveSettings_WhenNonDefaultTenantHasNoSettings_DoesNotFallBackToTheSharedHostDefault()
    {
        // Arrange
        // A non-default tenant with no Asterisk settings of its own must NOT borrow the host-level default
        // connection. Sharing that single ARI application across tenants would cross-deliver Stasis events between
        // tenants, so the provider must fail closed instead.
        var handler = new StubHttpMessageHandler(HttpStatusCode.NoContent);
        var client = CreateClientWithHostDefault(handler, shellName: "TenantA");

        // Act
        var exception = await Record.ExceptionAsync(() =>
            client.AddChannelToBridgeAsync("bridge-1", "channel-1", TestContext.Current.CancellationToken));

        // Assert
        Assert.IsType<AsteriskAriException>(exception);
    }

    [Fact]
    public async Task ResolveSettings_WhenDefaultShellHasNoTenantSettings_UsesTheSharedHostDefault()
    {
        // Arrange
        // Only the default shell may fall back to the single shared host connection, so a configured host default
        // resolves and the operation reaches Asterisk.
        var handler = new StubHttpMessageHandler(HttpStatusCode.NoContent);
        var client = CreateClientWithHostDefault(handler, shellName: "Default");

        // Act
        var exception = await Record.ExceptionAsync(() =>
            client.AddChannelToBridgeAsync("bridge-1", "channel-1", TestContext.Current.CancellationToken));

        // Assert
        Assert.Null(exception);
    }

    private static AsteriskAriClient CreateClientWithHostDefault(StubHttpMessageHandler handler, string shellName)
    {
        var defaultOptions = new DefaultAsteriskOptions
        {
            IsEnabled = true,
            BaseUrl = BaseUrl,
            UserName = "ari-user",
            Password = "secret",
            ApplicationName = "crestapps-telephony",
            TimeoutSeconds = 30,
        };

        var shellSettings = new ShellSettings { Name = shellName };
        var options = Options.Create(defaultOptions);
        var gate = new AsteriskAriApplicationGate(
            new AsteriskAriApplicationOwnershipRegistry(),
            shellSettings,
            options);

        return new AsteriskAriClient(
            SiteServiceFactory.Create(new AsteriskSettings()),
            new EphemeralDataProtectionProvider(),
            new StubHttpClientFactory(handler),
            options,
            shellSettings,
            gate,
            NullLogger<AsteriskAriClient>.Instance);
    }
}
