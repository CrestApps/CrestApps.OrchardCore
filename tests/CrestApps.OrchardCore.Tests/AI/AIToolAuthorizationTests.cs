using System.Security.Claims;
using CrestApps.OrchardCore.AI.Agent.Recipes;
using CrestApps.OrchardCore.AI.Agent.Tenants;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Environment.Shell;
using Xunit;

namespace CrestApps.OrchardCore.Tests.AI;

/// <summary>
/// SEC-A3 step 1: the tools a profile may use were filtered by permission only when the editor was drawn, so a posted
/// key the editor couldn't see was saved; and the tenant and recipe tools ran with system authority for whoever drove
/// the chat. Posted keys are now filtered on save, and those tools check the caller's own permission.
/// </summary>
public sealed class AIToolAuthorizationTests
{
    [Fact]
    public async Task FilterAsync_DropsAPostedToolTheEditorMayNotUse()
    {
        var names = await AIToolSelectionFilter.FilterAsync(
            posted: ["importOrchardCoreRecipe", "search"],
            existing: [],
            selectable: ["importOrchardCoreRecipe", "search"],
            isAuthorized: key => Task.FromResult(key == "search"));

        Assert.Equal(["search"], names);
    }

    [Fact]
    public async Task FilterAsync_KeepsAToolAlreadyOnTheProfileThatTheEditorCantSee()
    {
        var names = await AIToolSelectionFilter.FilterAsync(
            posted: ["search"],
            existing: ["importOrchardCoreRecipe"],
            selectable: ["importOrchardCoreRecipe", "search"],
            isAuthorized: key => Task.FromResult(key == "search"));

        Assert.Equal(["importOrchardCoreRecipe", "search"], names.Order());
    }

    [Fact]
    public async Task FilterAsync_LetsTheEditorRemoveAToolTheyMayUse()
    {
        var names = await AIToolSelectionFilter.FilterAsync(
            posted: [],
            existing: ["search"],
            selectable: ["search"],
            isAuthorized: _ => Task.FromResult(true));

        Assert.Empty(names);
    }

    [Fact]
    public async Task GetUnauthorizedAdditionsAsync_ReportsNewToolsTheCallerMayNotUse()
    {
        var refused = await AIToolSelectionFilter.GetUnauthorizedAdditionsAsync(
            requested: ["importOrchardCoreRecipe", "search", "legacy"],
            existing: ["legacy"],
            isAuthorized: key => Task.FromResult(key == "search"));

        Assert.Equal(["importOrchardCoreRecipe"], refused);
    }

    [Fact]
    public async Task CreateTenantTool_WithoutManageTenants_RefusesAndCreatesNothing()
    {
        var result = await new CreateTenantTool().InvokeAsync(Arguments(authorized: false, new() { ["name"] = "Evil", ["recipeName"] = "Blog" }), TestContext.Current.CancellationToken);

        Assert.Contains("not allowed", result?.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ImportOrchardTool_WithoutManageRecipes_RefusesAndImportsNothing()
    {
        var result = await new ImportOrchardTool().InvokeAsync(Arguments(authorized: false, new() { ["recipe"] = "{\"steps\":[]}" }), TestContext.Current.CancellationToken);

        Assert.Contains("not allowed", result?.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ImportOrchardTool_ForAnAnonymousVisitor_Refuses()
    {
        var result = await new ImportOrchardTool().InvokeAsync(Arguments(authorized: true, new() { ["recipe"] = "{\"steps\":[]}" }, authenticated: false), TestContext.Current.CancellationToken);

        Assert.Contains("not allowed", result?.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static AIFunctionArguments Arguments(bool authorized, Dictionary<string, object> values, bool authenticated = true)
    {
        var identity = authenticated
            ? new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")], "Test")
            : new ClaimsIdentity();

        var authorization = new Mock<IAuthorizationService>();
        authorization
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(authorized ? AuthorizationResult.Success() : AuthorizationResult.Failed());

        // Nothing that creates a tenant or runs a recipe is registered: reaching it would throw.
        var services = new ServiceCollection()
            .AddSingleton(authorization.Object)
            .AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } })
            .AddSingleton(new ShellSettings { Name = ShellSettings.DefaultShellName })
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .BuildServiceProvider();

        return new AIFunctionArguments(values) { Services = services };
    }
}
