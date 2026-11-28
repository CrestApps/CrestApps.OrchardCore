using System.Globalization;
using CrestApps.Core.AI.Tooling;
using CrestApps.OrchardCore.AI.Agent.AuditTrail;
using CrestApps.OrchardCore.AI.Agent.Communications;
using CrestApps.OrchardCore.AI.Agent.Notifications;
using CrestApps.OrchardCore.AI.Agent.OpenId;
using CrestApps.OrchardCore.AI.Agent.RateLimits;
using CrestApps.OrchardCore.AI.Agent.Secrets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Agent;

/// <summary>
/// The Orchard Core tools of the AI Agent module for OpenID Connect, rate limits, secrets, the audit trail,
/// notifications and the test email and SMS are registered with a title, a description and a category, can be picked
/// on a profile, and ask for approval exactly where the tool coverage plan says so: every tool that changes security
/// or sends something outside the site.
/// </summary>
public sealed class OrchardSecurityToolRegistrationTests
{
    private static readonly string[] _toolNames =
    [
        ListOpenIdApplicationsTool.TheName,
        GetOpenIdApplicationTool.TheName,
        ListOpenIdScopesTool.TheName,
        ListRateLimitPoliciesTool.TheName,
        SetRateLimitPolicyEnabledTool.TheName,
        ListSecretsTool.TheName,
        ListAuditTrailEventsTool.TheName,
        GetAuditTrailEventTool.TheName,
        ListNotificationsTool.TheName,
        MarkNotificationsTool.TheName,
        SendTestEmailTool.TheName,
        SendTestSmsTool.TheName,
    ];

    public static TheoryData<string, bool> Tools => new()
    {
        { ListOpenIdApplicationsTool.TheName, false },
        { GetOpenIdApplicationTool.TheName, false },
        { ListOpenIdScopesTool.TheName, false },
        { ListRateLimitPoliciesTool.TheName, false },
        { SetRateLimitPolicyEnabledTool.TheName, true },
        { ListSecretsTool.TheName, false },
        { ListAuditTrailEventsTool.TheName, false },
        { GetAuditTrailEventTool.TheName, false },
        { ListNotificationsTool.TheName, false },
        { MarkNotificationsTool.TheName, false },
        { SendTestEmailTool.TheName, true },
        { SendTestSmsTool.TheName, true },
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
    [InlineData(typeof(CrestApps.OrchardCore.AI.Agent.OpenIdToolsStartup), 3)]
    [InlineData(typeof(CrestApps.OrchardCore.AI.Agent.RateLimitsToolsStartup), 2)]
    [InlineData(typeof(CrestApps.OrchardCore.AI.Agent.SecretsToolsStartup), 1)]
    [InlineData(typeof(CrestApps.OrchardCore.AI.Agent.AuditTrailToolsStartup), 2)]
    [InlineData(typeof(CrestApps.OrchardCore.AI.Agent.NotificationsToolsStartup), 2)]
    [InlineData(typeof(CrestApps.OrchardCore.AI.Agent.EmailToolsStartup), 1)]
    [InlineData(typeof(CrestApps.OrchardCore.AI.Agent.SmsToolsStartup), 1)]
    public void EachFeatureStartup_RegistersItsTools(Type startupType, int expectedCount)
    {
        // Act
        var tools = GetTools(startupType);

        // Assert
        Assert.Equal(expectedCount, tools.Count);
        Assert.All(tools.Keys, name => Assert.Contains(name, _toolNames));
    }

    [Fact]
    public void TheExistingMessagingTools_KeepTheirNames()
    {
        // Arrange
        var tools = GetTools();

        // Act
        var names = tools.Keys;

        // Assert: the test tools are added beside the tools that send a message a person writes.
        Assert.Contains(SendEmailTool.TheName, names);
        Assert.Contains(SendSmsTool.TheName, names);
        Assert.Contains(SendNotificationTool.TheName, names);
    }

    [Theory]
    [InlineData("getOpenIdSettings")]
    [InlineData("updateOpenIdSettings")]
    [InlineData("getCorsSettings")]
    [InlineData("updateCorsSettings")]
    public void AToolThatWaitsForTheSettingsTools_IsNotRegistered(string toolName)
    {
        // Arrange
        var tools = GetTools();

        // Act
        var registered = tools.ContainsKey(toolName);

        // Assert
        Assert.False(registered);
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
