using System.Globalization;
using System.Security.Claims;
using CrestApps.Core.AI.Tooling;
using CrestApps.OrchardCore.AI.Agent.Contents;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.Tests.Agent;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Contents;
using OrchardCore.Modules;
using OrchardCore.Security;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Agent;

/// <summary>
/// C12 (review 11, SEC-A3): most AI agent tools ran with the system's authority, so a visitor of a public profile, or a
/// poisoned document in an administrator's chat, could delete content, disable features, send e-mail or read users. Every
/// agent tool now has a permission it checks against the person driving the conversation before it acts, and no
/// signed-in caller means no.
/// </summary>
public sealed class AgentToolCallerPermissionTests
{
    public static TheoryData<string> GuardedTools()
    {
        var data = new TheoryData<string>();

        foreach (var type in AgentToolTypes().Where(type => type != typeof(CrestApps.OrchardCore.AI.Agent.System.ListTimeZoneTool)))
        {
            data.Add(type.FullName);
        }

        return data;
    }

    [Fact]
    public void EveryAgentTool_HasACallerPermission_OrIsMarkedSafeForAnyone()
    {
        // Arrange
        using var provider = BuildAgentServices().BuildServiceProvider();
        var tools = provider.GetRequiredService<IOptions<AIToolDefinitionOptions>>().Value.Tools;
        var permissions = provider.GetRequiredService<IOptions<AIToolPermissionOptions>>().Value;
        var anonymous = provider.GetRequiredService<IOptions<AnonymousAIToolOptions>>().Value;

        // Act
        var agentTools = tools.Where(tool => tool.Value.ToolType.Assembly == typeof(CrestApps.OrchardCore.AI.Agent.Startup).Assembly).ToArray();
        var unguarded = agentTools.Where(tool => permissions.GetPermission(tool.Key) is null && !anonymous.IsSafe(tool.Key)).Select(tool => tool.Key);

        // Assert
        Assert.Equal(AgentToolTypes().Count(), agentTools.Length);
        Assert.Empty(unguarded);
        Assert.True(anonymous.IsSafe("listTimeZones"));
    }

    [Theory]
    [MemberData(nameof(GuardedTools))]
    public async Task AnAgentTool_RefusesACallerWhoIsNotSignedIn(string toolType)
    {
        // Arrange: authorization would grant anything; the caller simply isn't signed in.
        var services = BuildAgentServices().AddAllowingAuthorization();
        services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = new DefaultHttpContext() });

        // Act
        var result = await InvokeAsync(toolType, services);

        // Assert
        Assert.Equal(AIToolCallerAuthorization.NotAllowedMessage, result);
    }

    [Theory]
    [MemberData(nameof(GuardedTools))]
    public async Task AnAgentTool_RefusesASignedInCallerWithoutItsPermission(string toolType)
    {
        // Arrange
        var services = BuildAgentServices();
        var authorization = new Mock<IAuthorizationService>();
        authorization
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(AuthorizationResult.Failed());
        services.AddSingleton(authorization.Object);
        services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = AuthorizedCallerServiceCollectionExtensions.SignedInUser() } });

        // Act
        var result = await InvokeAsync(toolType, services);

        // Assert
        Assert.Equal(AIToolCallerAuthorization.NotAllowedMessage, result);
    }

    [Fact]
    public async Task DeleteContent_ByAnAuthor_DeletesOnlyTheirOwnItem()
    {
        // Arrange: an author may delete their own content (DeleteOwnContent) but not someone else's.
        var theirs = new ContentItem { ContentItemId = "theirs", ContentType = "Article", Owner = "someone-else" };
        var own = new ContentItem { ContentItemId = "own", ContentType = "Article", Owner = "admin" };
        var contentManager = new Mock<IContentManager>();
        contentManager.Setup(manager => manager.GetAsync("theirs", It.IsAny<VersionOptions>())).ReturnsAsync(theirs);
        contentManager.Setup(manager => manager.GetAsync("own", It.IsAny<VersionOptions>())).ReturnsAsync(own);

        var services = BuildAgentServices();
        services.AddSingleton(contentManager.Object);
        services.AddSingleton<ILogger<DeleteContentTool>>(NullLogger<DeleteContentTool>.Instance);
        services.AddSingleton<IAuthorizationService>(new AuthorAuthorizationService());
        services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = AuthorizedCallerServiceCollectionExtensions.SignedInUser() } });
        using var provider = services.BuildServiceProvider();

        // Act
        var refused = await new DeleteContentTool().InvokeAsync(new AIFunctionArguments(new Dictionary<string, object> { ["contentItemId"] = "theirs" }) { Services = provider }, TestContext.Current.CancellationToken);
        var deleted = await new DeleteContentTool().InvokeAsync(new AIFunctionArguments(new Dictionary<string, object> { ["contentItemId"] = "own" }) { Services = provider }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(AIToolCallerAuthorization.NotAllowedMessage, refused?.ToString());
        Assert.NotEqual(AIToolCallerAuthorization.NotAllowedMessage, deleted?.ToString());
        contentManager.Verify(manager => manager.RemoveAsync(theirs), Times.Never);
        contentManager.Verify(manager => manager.RemoveAsync(own), Times.Once);
    }

    private static async Task<string> InvokeAsync(string toolType, IServiceCollection services)
    {
        using var provider = services.BuildServiceProvider();
        var tool = (AIFunction)Activator.CreateInstance(typeof(CrestApps.OrchardCore.AI.Agent.Startup).Assembly.GetType(toolType, throwOnError: true));

        try
        {
            return (await tool.InvokeAsync(new AIFunctionArguments(new Dictionary<string, object>()) { Services = provider }, TestContext.Current.CancellationToken))?.ToString();
        }
        catch (Exception exception)
        {
            // A tool that reaches for its services before it checks the caller fails here, which is the bug.
            return $"{tool.Name} ran before checking the caller: {exception.GetType().Name}: {exception.Message}";
        }
    }

    private static IEnumerable<Type> AgentToolTypes()
        => typeof(CrestApps.OrchardCore.AI.Agent.Startup).Assembly.GetTypes().Where(type => !type.IsAbstract && typeof(AIFunction).IsAssignableFrom(type));

    // The tools are registered by the module's feature startups (Contents, Tenants, Recipes...); run them all.
    private static ServiceCollection BuildAgentServices()
    {
        var services = new ServiceCollection();

        foreach (var type in typeof(CrestApps.OrchardCore.AI.Agent.Startup).Assembly.GetTypes().Where(type => typeof(StartupBase).IsAssignableFrom(type) && !type.IsAbstract))
        {
            var constructor = type.GetConstructors().Single();
            var arguments = constructor.GetParameters()
                .Select(parameter => Activator.CreateInstance(typeof(PassThroughLocalizer<>).MakeGenericType(parameter.ParameterType.GetGenericArguments()[0])))
                .ToArray();

            ((StartupBase)constructor.Invoke(arguments)).ConfigureServices(services);
        }

        return services;
    }

    // Orchard Core's rule for content: DeleteContent on someone's own item is satisfied by DeleteOwnContent
    // (ContentTypeAuthorizationHandler); the author here holds DeleteOwnContent only.
    private sealed class AuthorAuthorizationService : IAuthorizationService
    {
        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements)
        {
            var permission = requirements.OfType<PermissionRequirement>().Single().Permission;
            var granted = permission.Name == CommonPermissions.DeleteOwnContent.Name ||
                (permission.Name == CommonPermissions.DeleteContent.Name && resource is ContentItem item && item.Owner == user.FindFirstValue(ClaimTypes.NameIdentifier));

            return Task.FromResult(granted ? AuthorizationResult.Success() : AuthorizationResult.Failed());
        }

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, string policyName)
            => Task.FromResult(AuthorizationResult.Failed());
    }

    private sealed class PassThroughLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name]
            => new(name, name);

        public LocalizedString this[string name, params object[] arguments]
            => new(name, string.Format(CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
            => [];
    }
}
