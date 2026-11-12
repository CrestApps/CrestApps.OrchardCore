using System.Reflection;
using System.Security.Claims;
using CrestApps.Core;
using CrestApps.Core.AI.Mcp;
using CrestApps.Core.AI.Mcp.Models;
using CrestApps.OrchardCore.AI.Mcp.Core;
using CrestApps.OrchardCore.AI.Mcp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules.Manifest;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Mcp;

/// <summary>
/// SEC-A1: the MCP local (stdio) transport starts an operating-system process. A tenant's administrator is not the
/// host's operator, so a non-default tenant must never be able to start one, a recipe must not import a stdio
/// connection for a caller without the dedicated permission, and the host can limit which commands may run at all.
/// </summary>
public sealed class McpStdioTenantGuardTests
{
    [Fact]
    public void StdioFeature_IsDefaultTenantOnly()
    {
        var feature = typeof(McpStdioOptions).Assembly
            .GetCustomAttributes<FeatureAttribute>()
            .Single(attribute => attribute.Id == McpPermissions.Feature.Stdio);

        Assert.True(feature.DefaultTenantOnly);
    }

    [Fact]
    public void StdioPermission_IsSecurityCritical()
    {
        Assert.True(McpPermissions.ManageMcpStdioConnections.IsSecurityCritical);
    }

    [Fact]
    public async Task GetAsync_OnANonDefaultTenant_RefusesToStartAProcess()
    {
        var provider = CreateProvider(tenantName: "Customer1");

        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GetAsync(CreateConnection("cmd.exe")));
    }

    [Fact]
    public async Task GetAsync_WithACommandOutsideTheAllowlist_Refuses()
    {
        var provider = CreateProvider(ShellSettings.DefaultShellName, allowedCommands: ["npx"]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GetAsync(CreateConnection("cmd.exe")));
    }

    [Fact]
    public async Task GetAsync_OnTheDefaultTenant_WithAnAllowedCommand_ReturnsATransport()
    {
        var provider = CreateProvider(ShellSettings.DefaultShellName, allowedCommands: ["npx"]);

        Assert.NotNull(await provider.GetAsync(CreateConnection("npx")));
    }

    [Fact]
    public async Task CanManageAsync_OnANonDefaultTenant_IsFalseEvenForAnAdministrator()
    {
        var authorization = new Mock<IAuthorizationService>();
        authorization
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(AuthorizationResult.Success());

        var allowed = await McpStdioAccess.CanManageAsync(new ShellSettings { Name = "Customer1" }, authorization.Object, CreateUser());

        Assert.False(allowed);
    }

    [Fact]
    public async Task CanManageAsync_WithoutThePermission_IsFalse()
    {
        var authorization = new Mock<IAuthorizationService>();
        authorization
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(AuthorizationResult.Failed());

        var allowed = await McpStdioAccess.CanManageAsync(new ShellSettings { Name = ShellSettings.DefaultShellName }, authorization.Object, CreateUser());

        Assert.False(allowed);
    }

    private static GuardedStdioClientTransportProvider CreateProvider(string tenantName, string[] allowedCommands = null)
        => new(
            new ShellSettings { Name = tenantName },
            Options.Create(new McpStdioOptions { AllowedCommands = allowedCommands ?? [] }),
            NullLogger<GuardedStdioClientTransportProvider>.Instance);

    private static McpConnection CreateConnection(string command)
    {
        var connection = new McpConnection
        {
            ItemId = "connection-1",
            DisplayText = "Local",
            Source = McpConstants.TransportTypes.StdIo,
        };

        connection.Put(new StdioMcpConnectionMetadata { Command = command, Arguments = [] });

        return connection;
    }

    private static ClaimsPrincipal CreateUser()
        => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")], "Test"));
}
