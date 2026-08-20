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
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Telephony;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
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
/// Registers the Contact Center Outbound Dialer feature: dialing profiles and their administration, callbacks,
/// agent-driven activity batch sources, and the mandatory eligibility and suppression compliance gate evaluated
/// before every outbound dialing attempt.
/// </summary>
[Feature(ContactCenterConstants.Feature.Dialer)]
public sealed class DialerStartup : StartupBase
{
    private readonly IStringLocalizer S;
    private readonly IShellConfiguration _shellConfiguration;

    public DialerStartup(
        IStringLocalizer<DialerStartup> stringLocalizer,
        IShellConfiguration shellConfiguration)
    {
        S = stringLocalizer;
        _shellConfiguration = shellConfiguration;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<IDialerProfileStore, DialerProfileStore>()
            .AddScoped<IDialerProfileManager, DialerProfileManager>()
            .AddScoped<ICallbackRequestStore, CallbackRequestStore>()
            .AddScoped<ICallbackRequestManager, CallbackRequestManager>()
            .AddScoped<IContactCenterRetentionPolicy, CallbackRequestRetentionPolicy>()
            .AddScoped<ICallbackService, CallbackService>()
            .AddScoped<IDialerService, DialerService>()
            .AddScoped<IActivityDialerContributor, ContactCenterActivityDialerContributor>()
            .AddScoped<IDialerStrategyResolver, DialerStrategyResolver>()
            .AddScoped<IContactCenterFeatureLifecycleParticipant>(serviceProvider =>
                new ContactCenterFeatureWorkLifecycleParticipant(
                    ContactCenterConstants.Feature.Dialer,
                    serviceProvider.GetRequiredService<IContactCenterFeatureWorkManager>(),
                    serviceProvider.GetRequiredService<IOptions<ContactCenterFeatureLifecycleOptions>>()));

        services
            .AddScoped<ICatalogEntryHandler<DialerProfile>, DialerProfileHandler>()
            .AddIndexProvider<DialerProfileIndexProvider>()
            .AddDataMigration<DialerProfileIndexMigrations>()
            .AddIndexProvider<CallbackRequestIndexProvider>()
            .AddDataMigration<CallbackRequestIndexMigrations>();

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBackgroundTask, CallbackDispatchBackgroundTask>());

        services.Configure<ActivityBatchSourceOptions>(options =>
        {
            options.AddSource(ActivitySources.Dialer, entry =>
            {
                entry.DisplayName = S["Dialer"];
                entry.Description = S["Loads unassigned activities and applies the selected dialer profile when the batch is loaded."];
                entry.RequiresUserAssignment = false;
            });

            options.AddSource(ActivitySources.PreviewDial, entry =>
            {
                entry.DisplayName = S["Preview dial batch"];
                entry.Description = S["Loads unassigned activities the dialer offers to agents one at a time for review before dialing."];
                entry.RequiresUserAssignment = false;
                entry.ShowInCreationPicker = false;
            });

        });

        // Outbound dialer administration screens.
        services.AddDisplayDriver<DialerProfile, DialerProfileDisplayDriver>();
        services.AddNavigationProvider<ContactCenterDialerAdminMenu>();

        // Mandatory eligibility and suppression gate evaluated before outbound dialing attempts.
        services
            .AddOptions<ContactCenterComplianceOptions>()
            .Bind(_shellConfiguration.GetSection("CrestApps:ContactCenter:Compliance"))
            .Validate(
                options => options.AbandonmentRollingWindowMinutes is >= 1 and <= 1440,
                "The Contact Center abandonment rolling window must be between 1 and 1440 minutes.")
            .ValidateOnStart();

        services
            .AddOptions<ManualDialingComplianceOptions>()
            .Bind(_shellConfiguration.GetSection("CrestApps:ContactCenter:Compliance:ManualDialing"))
            .Validate(
                options => !options.EnforceCallingWindow || !string.IsNullOrWhiteSpace(options.CallingCalendarId),
                "Manual dialing calling-window enforcement requires a calling calendar id.")
            .ValidateOnStart();

        services
            .AddScoped<IDialerAbandonmentPolicyService, DefaultDialerAbandonmentPolicyService>()
            .AddScoped<IDialerEligibilityService, DefaultDialerEligibilityService>()
            .AddScoped<IProviderCommandDispatchValidator, DialerProviderCommandDispatchValidator>()
            .AddScoped<IDialerAttemptCompensationService, DialerAttemptCompensationService>()
            .AddScoped<IDialerAttemptService, DialerAttemptService>()
            .AddScoped<IOutboundCallScreener, ContactCenterManualCallScreener>();
    }
}

/// <summary>
/// Registers the deployment steps that export the dialer profiles owned by the dialer feature.
/// </summary>
[Feature(ContactCenterConstants.Feature.Dialer)]
[RequireFeatures("OrchardCore.Deployment")]
public sealed class DialerDeploymentStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<ContactCenterDialerProfileDeploymentSource, ContactCenterDialerProfileDeploymentStep>();
    }
}

/// <summary>
/// Registers the recipe steps that import the dialer profiles owned by the dialer feature.
/// </summary>
[Feature(ContactCenterConstants.Feature.Dialer)]
[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class DialerRecipesStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<ContactCenterDialerProfileStep>();
    }
}

/// <summary>
/// Registers the Schedule Callback workflow task, available only when both Orchard Core Workflows and the
/// Dialer feature are enabled so the required callback service is always resolvable.
/// </summary>
[Feature(ContactCenterConstants.Feature.Dialer)]
[RequireFeatures("OrchardCore.Workflows")]
public sealed class ContactCenterDialerWorkflowsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddActivity<ScheduleCallbackTask, ScheduleCallbackTaskDisplayDriver>();
    }
}
