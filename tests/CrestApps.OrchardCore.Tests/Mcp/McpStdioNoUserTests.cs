using CrestApps.OrchardCore.AI.Mcp.Services;
using Microsoft.AspNetCore.Authorization;
using Moq;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Models;

namespace CrestApps.OrchardCore.Tests.Mcp;

/// <summary>
/// Review 11 (Mild): a recipe that ran with no principal at all was treated as the host itself on the default tenant,
/// so it could add a local (stdio) MCP connection without <c>ManageMcpStdioConnections</c>. Orchard Core gives a
/// background task or deferred recipe its own context, with an anonymous principal (or the user who queued it), so no
/// principal is not a sign of the host. Only setup, while the tenant initializes, still runs as the host.
/// </summary>
public sealed class McpStdioNoUserTests
{
    [Fact]
    public async Task CanManageAsync_WithNoPrincipal_OnARunningDefaultTenant_IsFalse()
    {
        var settings = new ShellSettings { Name = ShellSettings.DefaultShellName, State = TenantState.Running };

        var allowed = await McpStdioAccess.CanManageAsync(settings, Mock.Of<IAuthorizationService>(), user: null);

        Assert.False(allowed);
    }

    [Fact]
    public async Task CanManageAsync_WithNoPrincipal_WhileTheDefaultTenantIsSetUp_IsTrue()
    {
        var settings = new ShellSettings { Name = ShellSettings.DefaultShellName, State = TenantState.Initializing };

        var allowed = await McpStdioAccess.CanManageAsync(settings, Mock.Of<IAuthorizationService>(), user: null);

        Assert.True(allowed);
    }
}
