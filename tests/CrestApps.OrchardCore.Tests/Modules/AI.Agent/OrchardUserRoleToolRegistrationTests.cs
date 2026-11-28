using System.Globalization;
using CrestApps.Core.AI.Tooling;
using CrestApps.OrchardCore.AI.Agent.Roles;
using CrestApps.OrchardCore.AI.Agent.Users;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Agent;

/// <summary>
/// The Orchard Core tools of the AI Agent module for users and roles are registered with a title, a description and a
/// category, can be picked on a profile, and ask for approval where the change is hard to undo: saving a user, a
/// user's roles or a role, resetting a user's sign-in factors, and every delete. Explaining a user's access only reads. Enabling, disabling and unlocking an
/// account can be undone and confirming its email is low risk, so they do not ask.
/// </summary>
public sealed class OrchardUserRoleToolRegistrationTests
{
    public static TheoryData<string, bool> Tools => new()
    {
        { ListUsersTool.TheName, false },
        { GetUserTool.TheName, false },
        { SaveUserTool.TheName, true },
        { SetUserRolesTool.TheName, true },
        { ExplainUserAccessTool.TheName, false },
        { SetUserAccountStateTool.TheName, true },
        { DeleteUserTool.TheName, true },
        { ResetUserSignInFactorsTool.TheName, true },
        { ListRolesTool.TheName, false },
        { GetRoleDetailsTool.TheName, false },
        { ListPermissionsTool.TheName, false },
        { SaveRoleTool.TheName, true },
        { DeleteRoleTool.TheName, true },
    };

    [Theory]
    [MemberData(nameof(Tools))]
    public void Tool_IsRegisteredWithItsTextsAndApproval(string toolName, bool requiresApproval)
    {
        // Arrange
        var tools = GetTools();

        // Act
        var found = tools.TryGetValue(toolName, out var tool);

        // Assert
        Assert.True(found, $"The tool '{toolName}' is not registered.");
        Assert.Equal(toolName, tool.Name);
        Assert.False(string.IsNullOrWhiteSpace(tool.Title));
        Assert.NotEqual(toolName, tool.Title);
        Assert.False(string.IsNullOrWhiteSpace(tool.Description));
        Assert.NotEqual(toolName, tool.Description);
        Assert.False(string.IsNullOrWhiteSpace(tool.Category));
        Assert.True(tool.IsSelectable());
        Assert.Equal(requiresApproval, tool.RequiresApproval);
    }

    [Theory]
    [InlineData(typeof(CrestApps.OrchardCore.AI.Agent.UserRolesToolsStartup), SetUserRolesTool.TheName, 2)]
    [InlineData(typeof(CrestApps.OrchardCore.AI.Agent.UserRolesToolsStartup), ExplainUserAccessTool.TheName, 2)]
    [InlineData(typeof(CrestApps.OrchardCore.AI.Agent.UsersTwoFactorToolsStartup), ResetUserSignInFactorsTool.TheName, 1)]
    public void Tool_ThatNeedsAnotherFeature_IsRegisteredByTheStartupOfThatFeatureOnly(Type startupType, string toolName, int featureToolCount)
    {
        // Arrange
        var users = GetTools(typeof(CrestApps.OrchardCore.AI.Agent.UsersToolsStartup));
        var feature = GetTools(startupType);

        // Act
        var registeredWithUsers = users.ContainsKey(toolName);
        var registeredWithFeature = feature.ContainsKey(toolName);

        // Assert
        Assert.False(registeredWithUsers);
        Assert.True(registeredWithFeature);
        Assert.Equal(featureToolCount, feature.Count);
    }

    [Fact]
    public void TheExistingUserAndRoleTools_KeepTheirNames()
    {
        // Arrange
        var tools = GetTools();

        // Act
        var names = tools.Keys;

        // Assert: the new tools are added beside the tools they will replace.
        Assert.Contains(GetUserInfoTool.TheName, names);
        Assert.Contains(SearchForUsersTool.TheName, names);
        Assert.Contains(GetRoleTool.TheName, names);
    }

    private static IReadOnlyDictionary<string, AIToolDefinitionEntry> GetTools(Type startupType = null)
    {
        var services = new ServiceCollection();

        // The tools are registered by the module's feature startups; run them all, or the one that is asked for.
        foreach (var type in typeof(CrestApps.OrchardCore.AI.Agent.Startup).Assembly.GetTypes()
            .Where(type => typeof(StartupBase).IsAssignableFrom(type) && !type.IsAbstract)
            .Where(type => startupType is null || type == startupType))
        {
            var constructor = type.GetConstructors().Single();
            var arguments = constructor.GetParameters()
                .Select(parameter => Activator.CreateInstance(typeof(PassThroughLocalizer<>).MakeGenericType(parameter.ParameterType.GetGenericArguments()[0])))
                .ToArray();

            ((StartupBase)constructor.Invoke(arguments)).ConfigureServices(services);
        }

        using var provider = services.BuildServiceProvider();

        return provider.GetRequiredService<IOptions<AIToolDefinitionOptions>>().Value.Tools;
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
