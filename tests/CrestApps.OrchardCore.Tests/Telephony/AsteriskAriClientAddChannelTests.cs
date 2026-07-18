using System.Net;
using CrestApps.OrchardCore.Asterisk;
using CrestApps.OrchardCore.Asterisk.Models;
using CrestApps.OrchardCore.Asterisk.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Tests.Telephony;

public sealed class AsteriskAriClientAddChannelTests
{
    private const string BaseUrl = "http://asterisk.example/ari/";
    private const string PlainPassword = "secret";

    [Fact]
    public async Task AddChannelToBridgeAsync_WhenAsteriskReturnsConflict_ThrowsSoTheCallerCanCompensate()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(HttpStatusCode.Conflict, """{"message":"Channel not in Stasis application"}""");
        var client = CreateClient(handler);

        // Act
        var exception = await Record.ExceptionAsync(() =>
            client.AddChannelToBridgeAsync("bridge-1", "channel-1", TestContext.Current.CancellationToken));

        // Assert
        Assert.IsType<AsteriskAriException>(exception);
    }

    [Fact]
    public async Task AddChannelToBridgeAsync_WhenAsteriskReturnsNoContent_SucceedsForIdempotentReAdds()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(HttpStatusCode.NoContent);
        var client = CreateClient(handler);

        // Act
        var exception = await Record.ExceptionAsync(() =>
            client.AddChannelToBridgeAsync("bridge-1", "channel-1", TestContext.Current.CancellationToken));

        // Assert
        Assert.Null(exception);
    }

    private static AsteriskAriClient CreateClient(StubHttpMessageHandler handler)
    {
        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var protectedPassword = dataProtectionProvider
            .CreateProtector(AsteriskConstants.ProtectorName)
            .Protect(PlainPassword);
        var settings = new AsteriskSettings
        {
            IsEnabled = true,
            BaseUrl = BaseUrl,
            UserName = "ari-user",
            Password = protectedPassword,
            ApplicationName = "crestapps-telephony",
            TimeoutSeconds = 30,
        };

        var shellSettings = new ShellSettings { Name = "Default" };
        var options = Options.Create(new DefaultAsteriskOptions());
        var gate = new AsteriskAriApplicationGate(
            new AsteriskAriApplicationOwnershipRegistry(),
            shellSettings,
            options);

        return new AsteriskAriClient(
            SiteServiceFactory.Create(settings),
            dataProtectionProvider,
            new StubHttpClientFactory(handler),
            options,
            shellSettings,
            gate,
            NullLogger<AsteriskAriClient>.Instance);
    }
}
