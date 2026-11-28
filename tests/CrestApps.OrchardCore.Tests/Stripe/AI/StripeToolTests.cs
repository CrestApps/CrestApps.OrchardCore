using CrestApps.OrchardCore.Stripe.AI;
using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Models;
using CrestApps.OrchardCore.Stripe.Services;
using CrestApps.OrchardCore.Tests.Agent;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Stripe.AI;

/// <summary>
/// The Stripe tools refuse a caller who is not signed in or lacks Manage Stripe settings, never return a key or a
/// secret, and disconnect only a named environment.
/// </summary>
public sealed class StripeToolTests
{
    private const string SecretCanary = "sk_test_CANARY_SECRET";

    private readonly Mock<IStripeConnectService> _connect = new();

    public static TheoryData<string> Tools => new()
    {
        GetStripeConnectionStatusTool.TheName,
        DisconnectStripeTool.TheName,
    };

    [Theory]
    [MemberData(nameof(Tools))]
    public async Task Tool_RefusesACallerWhoIsNotSignedIn(string toolName)
    {
        // Act
        var result = await InvokeAsync(toolName, Build(signedIn: false), new() { ["environment"] = "test" });

        // Assert
        Assert.Equal(StripeAIToolBase.NotAllowedMessage, result);
        Assert.Empty(_connect.Invocations);
    }

    [Theory]
    [MemberData(nameof(Tools))]
    public async Task Tool_RefusesACallerWithoutManageStripeSettings(string toolName)
    {
        // Arrange
        var services = Build(authorize: (asked, _) => asked.Name != StripePermissions.ManageStripeSettings.Name);

        // Act
        var result = await InvokeAsync(toolName, services, new() { ["environment"] = "test" });

        // Assert
        Assert.Equal(StripeAIToolBase.NotAllowedMessage, result);
        Assert.Empty(_connect.Invocations);
    }

    [Fact]
    public async Task GetStripeConnectionStatus_SaysWhichKeysAreSet_WithoutReturningThem()
    {
        // Arrange
        _connect.Setup(connect => connect.IsConnectedAsync(false)).ReturnsAsync(true);

        // Act
        var result = await InvokeAsync(GetStripeConnectionStatusTool.TheName, Build(), []);
        var json = AgentToolTestContext.ReadJson(result);

        // Assert
        Assert.DoesNotContain(SecretCanary, result, StringComparison.Ordinal);
        Assert.Equal("test", json.GetProperty("mode").GetString());
        Assert.True(json.GetProperty("test").GetProperty("connected").GetBoolean());
        Assert.True(json.GetProperty("test").GetProperty("hasSecretKey").GetBoolean());
        Assert.False(json.GetProperty("live").GetProperty("hasSecretKey").GetBoolean());
    }

    [Fact]
    public async Task DisconnectStripe_NeedsTheEnvironment()
    {
        // Act
        var result = await InvokeAsync(DisconnectStripeTool.TheName, Build(), new() { ["environment"] = "staging" });

        // Assert
        Assert.Equal("Nothing was done: environment must be live or test.", result);
        _connect.Verify(connect => connect.DisconnectAsync(It.IsAny<bool>()), Times.Never);
    }

    private ServiceProvider Build(bool signedIn = true, Func<Permission, object, bool> authorize = null)
    {
        var services = AgentToolTestContext.CreateServices(signedIn, authorize);
        var site = new Mock<ISite>();
        var siteService = new Mock<ISiteService>();

        site.Setup(value => value.GetOrCreate<StripeSettings>()).Returns(new StripeSettings
        {
            TestAccountId = "acct_test",
            TestPrivateSecret = SecretCanary,
            TestWebhookSecret = SecretCanary,
        });
        siteService.Setup(service => service.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

        services.AddSingleton(siteService.Object);
        services.AddSingleton(_connect.Object);
        services.AddSingleton(Options.Create(new StripeConfigurationOptions()));

        return services.BuildServiceProvider();
    }

    private static Task<string> InvokeAsync(string toolName, IServiceProvider services, Dictionary<string, object> arguments)
    {
        AIFunction tool = toolName == GetStripeConnectionStatusTool.TheName
            ? new GetStripeConnectionStatusTool()
            : new DisconnectStripeTool();

        return AgentToolTestContext.InvokeAsync(tool, services, arguments);
    }
}
