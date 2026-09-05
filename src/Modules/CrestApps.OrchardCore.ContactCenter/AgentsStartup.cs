using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.BackgroundTasks;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Services.Retention;
using CrestApps.OrchardCore.ContactCenter.Deployments.Sources;
using CrestApps.OrchardCore.ContactCenter.Deployments.Steps;
using CrestApps.OrchardCore.ContactCenter.Drivers;
using CrestApps.OrchardCore.ContactCenter.Handlers;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Migrations;
using CrestApps.OrchardCore.ContactCenter.Recipes;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.ContactCenter.Workflows.Drivers;
using CrestApps.OrchardCore.ContactCenter.Workflows.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Recipes;
using OrchardCore.Workflows.Helpers;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Registers the Contact Center Agents feature: agent profiles, skills, and state reason codes together with
/// their administration screens, and the durable agent presence, availability sessions, heartbeat recovery, and
/// logout synchronization that track who is working.
/// </summary>
[Feature(ContactCenterConstants.Feature.Agents)]
public sealed class AgentsStartup : StartupBase
{
    private readonly IShellConfiguration _shellConfiguration;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentsStartup"/> class.
    /// </summary>
    /// <param name="shellConfiguration">The shell configuration used to bind availability policy.</param>
    public AgentsStartup(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        // The agent-profile directory (store, manager, index) is provided by the Agent Services feature this
        // feature depends on, so it is not registered again here.
        services
            .AddScoped<IAgentStateReasonCodeStore, AgentStateReasonCodeStore>()
            .AddScoped<IAgentStateReasonCodeManager, AgentStateReasonCodeManager>();

        services
            .AddIndexProvider<AgentQueueMembershipIndexProvider>()
            .AddDataMigration<AgentQueueMembershipIndexMigrations>()
            .AddIndexProvider<AgentAllowedQueueIndexProvider>()
            .AddDataMigration<AgentAllowedQueueIndexMigrations>();

        services
            .AddScoped<ICatalogEntryHandler<AgentStateReasonCode>, AgentStateReasonCodeHandler>()
            .AddIndexProvider<AgentStateReasonCodeIndexProvider>()
            .AddDataMigration<AgentStateReasonCodeIndexMigrations>();

        // Agent administration screens.
        services.AddDisplayDriver<AgentStateReasonCode, AgentStateReasonCodeDisplayDriver>();
        services.AddNavigationProvider<ContactCenterAgentsAdminMenu>();

        // Durable agent presence, availability sessions, heartbeat recovery, and logout synchronization.
        services
            .AddOptions<AgentAvailabilityOptions>()
            .Bind(_shellConfiguration.GetSection("CrestApps:ContactCenter:Availability"))
            .Validate(options => options.HeartbeatTimeout > TimeSpan.Zero, "HeartbeatTimeout must be greater than zero.")
            .Validate(options => options.MaximumWrapUpDuration > TimeSpan.Zero, "MaximumWrapUpDuration must be greater than zero.")
            .ValidateOnStart();

        services
            // The permissive default: no entitlement restriction. The Agent Entitlements feature replaces this
            // with an enforcing policy when enabled.
            .AddScoped<IAgentEntitlementPolicy, PermissiveAgentEntitlementPolicy>()
            .AddScoped<IAgentPresenceManager, AgentPresenceManagerService>()
            .AddScoped<IActivityDispositionHandler, ContactCenterActivityDispositionHandler>()
            .AddScoped<IAgentSessionStore, AgentSessionStore>()
            .AddScoped<IAgentSessionManager, AgentSessionManager>()
            .AddScoped<IAgentSessionService, AgentSessionService>()
            .AddScoped<IAgentAvailabilityService, AgentAvailabilityService>()
            .AddScoped<IAgentAvailabilityRecoveryService, AgentAvailabilityRecoveryService>()
            .AddScoped<IContactCenterRetentionPolicy, AgentSessionRetentionPolicy>();

        services
            .AddIndexProvider<AgentSessionIndexProvider>()
            .AddDataMigration<AgentSessionIndexMigrations>();

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBackgroundTask, AgentSessionCleanupBackgroundTask>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBackgroundTask, AgentAvailabilityRecoveryBackgroundTask>());

        services.ConfigureOptions<ContactCenterAgentSignOutCookieConfiguration>();
    }
}

/// <summary>
/// Registers the deployment steps that export the agent configuration owned by the agents feature.
/// </summary>
[Feature(ContactCenterConstants.Feature.Agents)]
[RequireFeatures("OrchardCore.Deployment")]
public sealed class AgentsDeploymentStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<AgentStateReasonCodeDeploymentSource, AgentStateReasonCodeDeploymentStep>();
    }
}

/// <summary>
/// Registers the recipe steps that import the agent configuration owned by the agents feature.
/// </summary>
[Feature(ContactCenterConstants.Feature.Agents)]
[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class AgentsRecipesStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<AgentStateReasonCodeStep>();
    }
}

/// <summary>
/// Registers the Set Agent Presence workflow task, available only when both Orchard Core Workflows and the
/// Agents feature are enabled so the required presence service is always resolvable.
/// </summary>
[Feature(ContactCenterConstants.Feature.Agents)]
[RequireFeatures("OrchardCore.Workflows")]
public sealed class ContactCenterAvailabilityWorkflowsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddActivity<SetAgentPresenceTask, SetAgentPresenceTaskDisplayDriver>();
    }
}
