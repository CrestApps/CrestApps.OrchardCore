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

    /// <summary>
    /// A recipe entry that names an existing remote connection and sets its source to stdio would turn it into a local
    /// connection: the package copies <c>Source</c> from the entry on update. The step refuses a change of source.
    /// </summary>
    [Theory]
    [InlineData("stdio")]
    [InlineData("StdIo")]
    public async Task McpConnectionStep_RefusesToTurnARemoteConnectionIntoALocalOne(string source)
    {
        var connection = new McpConnection { ItemId = "connection-x", Source = McpConstants.TransportTypes.Sse };
        var manager = new Mock<CrestApps.Core.Services.ISourceCatalogManager<McpConnection>>();
        manager.Setup(x => x.FindByIdAsync("connection-x", It.IsAny<CancellationToken>())).Returns(() => ValueTask.FromResult(connection));
        manager.Setup(x => x.ValidateAsync(It.IsAny<McpConnection>(), It.IsAny<CancellationToken>())).Returns(() => ValueTask.FromResult(new CrestApps.Core.Models.ValidationResultDetails()));

        var authorization = new Mock<IAuthorizationService>();
        authorization
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(AuthorizationResult.Failed());

        var step = new CrestApps.OrchardCore.AI.Mcp.Recipes.McpConnectionStep(
            manager.Object,
            Options.Create(new McpClientAIOptions()),
            new ShellSettings { Name = ShellSettings.DefaultShellName },
            authorization.Object,
            new Microsoft.AspNetCore.Http.HttpContextAccessor { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext { User = CreateUser() } },
            new CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughStringLocalizer<CrestApps.OrchardCore.AI.Mcp.Recipes.McpConnectionStep>());

        var context = new global::OrchardCore.Recipes.Models.RecipeExecutionContext
        {
            Name = CrestApps.OrchardCore.AI.Mcp.Recipes.McpConnectionStep.StepKey,
            Step = new System.Text.Json.Nodes.JsonObject
            {
                ["Connections"] = new System.Text.Json.Nodes.JsonArray
                {
                    new System.Text.Json.Nodes.JsonObject
                    {
                        [nameof(McpConnection.ItemId)] = "connection-x",
                        [nameof(McpConnection.Source)] = source,
                        ["Properties"] = new System.Text.Json.Nodes.JsonObject
                        {
                            [nameof(StdioMcpConnectionMetadata)] = new System.Text.Json.Nodes.JsonObject { [nameof(StdioMcpConnectionMetadata.Command)] = "cmd.exe" },
                        },
                    },
                },
            },
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => step.ExecuteAsync(context));

        manager.Verify(x => x.UpdateAsync(It.IsAny<McpConnection>(), It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal(McpConstants.TransportTypes.Sse, connection.Source);
    }

    /// <summary>
    /// Setup runs its recipe under an anonymous principal, not a null one, while the tenant is being set up. It is the
    /// host's own operator, so a setup recipe may carry a local connection; an anonymous caller of a running tenant may
    /// not.
    /// </summary>
    [Theory]
    [InlineData(global::OrchardCore.Environment.Shell.Models.TenantState.Initializing, true)]
    [InlineData(global::OrchardCore.Environment.Shell.Models.TenantState.Running, false)]
    public async Task CanManageAsync_ForAnAnonymousPrincipal_AllowsOnlyTheSetupRecipe(global::OrchardCore.Environment.Shell.Models.TenantState state, bool expected)
    {
        var settings = new ShellSettings { Name = ShellSettings.DefaultShellName };
        settings.State = state;

        var allowed = await McpStdioAccess.CanManageAsync(settings, Mock.Of<IAuthorizationService>(), new ClaimsPrincipal(new ClaimsIdentity()));

        Assert.Equal(expected, allowed);
    }

    /// <summary>
    /// The allowlist names commands. A command given as a path is allowed only when that exact path is listed, so with
    /// <c>["npx"]</c> a <c>npx.exe</c> on a network share does not run.
    /// </summary>
    [Theory]
    [InlineData(@"\\host\share\npx.exe")]
    [InlineData(@"C:\Users\Public\npx.cmd")]
    [InlineData("./npx")]
    public async Task GetAsync_WithAPathWhoseFileNameIsAllowed_Refuses(string command)
    {
        var provider = CreateProvider(ShellSettings.DefaultShellName, allowedCommands: ["npx"]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GetAsync(CreateConnection(command)));
    }

    [Fact]
    public async Task GetAsync_WithAPathThatIsListed_ReturnsATransport()
    {
        var provider = CreateProvider(ShellSettings.DefaultShellName, allowedCommands: [@"C:\tools\npx.cmd"]);

        Assert.NotNull(await provider.GetAsync(CreateConnection(@"C:\tools\npx.cmd")));
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
