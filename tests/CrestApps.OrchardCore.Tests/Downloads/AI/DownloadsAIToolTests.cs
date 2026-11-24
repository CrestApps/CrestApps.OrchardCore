using CrestApps.OrchardCore.Downloads;
using CrestApps.OrchardCore.Downloads.AI;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Tests.Agent;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tests.Downloads.AI;

/// <summary>
/// The download tools need Manage digital files and downloads (code name <c>ManageDigitalDownloads</c>) and check their
/// arguments before they touch an entitlement. <c>DownloadService</c> reads entitlements through YesSql index queries,
/// so its revoke, reset and list paths are not run here.
/// </summary>
public sealed class DownloadsAIToolTests
{
    private readonly Mock<IOrderStore> _orders = new();

    public static TheoryData<string> ToolNames => new()
    {
        ListOrderDownloadsTool.TheName,
        SetOrderDownloadAccessTool.TheName,
    };

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task Tool_RefusesACallerWhoIsNotSignedIn(string toolName)
    {
        // Act
        var result = await InvokeAsync(toolName, Build(signedIn: false), Arguments(toolName));

        // Assert
        Assert.Equal(DownloadsAIToolBase.NotAllowedMessage, result);
        Assert.Empty(_orders.Invocations);
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task Tool_RefusesACallerWithoutManageDownloads(string toolName)
    {
        // Arrange
        var services = Build(authorize: (asked, _) => asked.Name != DownloadsPermissions.ManageDownloads.Name);

        // Act
        var result = await InvokeAsync(toolName, services, Arguments(toolName));

        // Assert
        Assert.Equal(DownloadsAIToolBase.NotAllowedMessage, result);
        Assert.Equal("ManageDigitalDownloads", Assert.Single(services.GetRequiredService<FakeAuthorizationService>().Checks).Permission);
        Assert.Empty(_orders.Invocations);
    }

    [Fact]
    public async Task ListOrderDownloads_SaysWhenNoOrderHasTheNumber()
    {
        // Arrange
        _orders.Setup(store => store.FindByNumberAsync("404", It.IsAny<CancellationToken>())).ReturnsAsync((Order)null);

        // Act
        var result = await InvokeAsync(ListOrderDownloadsTool.TheName, Build(), new() { ["orderNumber"] = "404" });
        var none = await InvokeAsync(ListOrderDownloadsTool.TheName, Build(), []);

        // Assert
        Assert.Equal("No order has the number '404'.", result);
        Assert.Equal("Give the order's id or its number.", none);
    }

    [Theory]
    [InlineData("extend", null, "action must be revoke or reset")]
    [InlineData("reset", "Charged back", "a reason is kept only when a download is revoked")]
    public async Task SetOrderDownloadAccess_RefusesWhatItCannotDo(string action, string reason, string expected)
    {
        // Arrange
        var arguments = new Dictionary<string, object> { ["entitlementId"] = "entitlement-1", ["action"] = action };

        if (reason is not null)
        {
            arguments["reason"] = reason;
        }

        // Act
        var result = await InvokeAsync(SetOrderDownloadAccessTool.TheName, Build(), arguments);

        // Assert
        Assert.StartsWith("Nothing was changed", result, StringComparison.Ordinal);
        Assert.Contains(expected, result, StringComparison.Ordinal);
    }

    private static Dictionary<string, object> Arguments(string toolName)
        => toolName switch
        {
            SetOrderDownloadAccessTool.TheName => new() { ["entitlementId"] = "entitlement-1", ["action"] = "revoke" },
            _ => new() { ["orderNumber"] = "1042" },
        };

    private ServiceProvider Build(bool signedIn = true, Func<Permission, object, bool> authorize = null)
    {
        var services = AgentToolTestContext.CreateServices(signedIn, authorize);

        services.AddSingleton(_orders.Object);

        return services.BuildServiceProvider();
    }

    private static Task<string> InvokeAsync(string toolName, IServiceProvider services, Dictionary<string, object> arguments)
    {
        AIFunction tool = toolName switch
        {
            ListOrderDownloadsTool.TheName => new ListOrderDownloadsTool(),
            _ => new SetOrderDownloadAccessTool(),
        };

        return AgentToolTestContext.InvokeAsync(tool, services, arguments);
    }
}
