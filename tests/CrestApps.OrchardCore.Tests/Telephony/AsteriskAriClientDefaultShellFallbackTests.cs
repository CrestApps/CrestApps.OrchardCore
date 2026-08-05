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
    public async Task ResolveSettings_WhenNonDefaultTenantHasNoSettings_FallsBackToTheSharedHostDefaultUnderUniqueApplicationName()
    {
        // Arrange
        // A non-default tenant with no Asterisk settings of its own falls back to the shared host-level default
        // connection. Each shell resolves it under a unique per-tenant ARI application name, so the fallback succeeds
        // without cross-delivering Stasis events with another tenant.
        var handler = new StubHttpMessageHandler(HttpStatusCode.NoContent);
        var client = CreateClientWithHostDefault(handler, shellName: "TenantA");

        // Act
        var exception = await Record.ExceptionAsync(() =>
            client.AddChannelToBridgeAsync("bridge-1", "channel-1", TestContext.Current.CancellationToken));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public async Task ResolveSettings_WhenDefaultShellHasNoTenantSettings_UsesTheSharedHostDefault()
    {
        // Arrange
        // The default shell also falls back to the shared host connection, resolved under its own per-tenant ARI
        // application name, so a configured host default resolves and the operation reaches Asterisk.
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
            new AsteriskAriApplicationOwnershipRegistry(NullLogger<AsteriskAriApplicationOwnershipRegistry>.Instance),
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
