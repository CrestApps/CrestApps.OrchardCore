using CrestApps.OrchardCore.ContactCenter.BackgroundTasks;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Services.Retention;
using CrestApps.OrchardCore.ContactCenter.Handlers;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Migrations;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Registers durable agent presence, availability sessions, heartbeat recovery, and logout synchronization
/// as part of the Contact Center Workforce feature.
/// </summary>
[Feature(ContactCenterConstants.Feature.Agents)]
public sealed class AvailabilityStartup : StartupBase
{
    private readonly IShellConfiguration _shellConfiguration;

    /// <summary>
    /// Initializes a new instance of the <see cref="AvailabilityStartup"/> class.
    /// </summary>
    /// <param name="shellConfiguration">The shell configuration used to bind availability policy.</param>
    public AvailabilityStartup(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddOptions<AgentAvailabilityOptions>()
            .Bind(_shellConfiguration.GetSection("CrestApps:ContactCenter:Availability"))
            .Validate(options => options.HeartbeatTimeout > TimeSpan.Zero, "HeartbeatTimeout must be greater than zero.")
            .Validate(options => options.MaximumWrapUpDuration > TimeSpan.Zero, "MaximumWrapUpDuration must be greater than zero.")
            .ValidateOnStart();

        services
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
