using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Core.Throttling;
using CrestApps.OrchardCore.YesSql.Core.Retention;
using CrestApps.OrchardCore.Wizard.Contents;
using CrestApps.OrchardCore.Wizard.Core.Indexes;
using CrestApps.OrchardCore.Wizard.Core.Migrations;
using CrestApps.OrchardCore.Wizard.Core.Models;
using CrestApps.OrchardCore.Wizard.Core.Services;
using CrestApps.OrchardCore.Wizard.Drivers;
using CrestApps.OrchardCore.Wizard.Handlers;
using CrestApps.OrchardCore.Wizard.Migrations;
using CrestApps.OrchardCore.Wizard.Services;
using CrestApps.OrchardCore.Wizard.Workflows.Drivers;
using CrestApps.OrchardCore.Wizard.Workflows.Handlers;
using CrestApps.OrchardCore.Wizard.Workflows.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentTypes.Editors;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.RateLimits;
using OrchardCore.Security.Permissions;
using OrchardCore.Workflows.Helpers;

namespace CrestApps.OrchardCore.Wizard;

/// <summary>
/// Registers the reusable, code-driven wizard framework services.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddScoped<GuestSessionTokenManager>();
        services.AddScoped<IWizardSessionStore, WizardSessionStore>();
        services.AddScoped<IWizardEngine, DefaultWizardEngine>();
        services.AddScoped<IWizardDefinitionProvider, DefaultWizardDefinitionProvider>();
        services.AddScoped<WizardResumeCookieManager>();
        services.AddScoped<IDisplayDriver<WizardFlow>, DefaultWizardFlowDisplayDriver>();
        services.AddPermissionProvider<WizardPermissionsProvider>();

        services.AddDataMigration<WizardMigrations>()
            .AddIndexProvider<WizardSessionIndexProvider>();

        // Pending sessions nobody came back to are removed by the platform retention task.
        services.AddRetentionPolicy<WizardSessionRetentionPolicy>();
    }
}

/// <summary>
/// Holds each visitor to <see cref="RequestsPerMinute"/> wizard requests a minute when Orchard Core's Rate Limits
/// feature is enabled. The wizard routes are public, and the first submission of a wizard stores a session.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.RateLimits)]
public sealed class RateLimitsStartup : StartupBase
{
    /// <summary>
    /// The wizard requests a visitor may make in a minute.
    /// </summary>
    public const int RequestsPerMinute = 60;

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<RateLimitsOptions>(options => options.AddClientGroupRateLimit(
            WizardConstants.RateLimitGroups.Wizard,
            RequestsPerMinute,
            TimeSpan.FromMinutes(1)));
    }
}

[RequireFeatures(OrchardCoreFeatureIds.Workflows)]
public sealed class WorkflowsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddActivity<WizardStartedEvent, WizardStartedEventDisplayDriver>();
        services.AddActivity<WizardStepDisplayedEvent, WizardStepDisplayedEventDisplayDriver>();
        services.AddActivity<WizardCompletedEvent, WizardCompletedEventDisplayDriver>();
        services.AddActivity<WizardFailedEvent, WizardFailedEventDisplayDriver>();

        services.AddScoped<IWizardHandler, WizardWorkflowHandler>();
    }
}

[RequireFeatures(OrchardCoreFeatureIds.Contents)]
public sealed class ContentsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContentPart<WizardPart>()
            .UseDisplayDriver<WizardPartDisplayDriver>()
            .AddHandler<WizardPartHandler>();

        services.AddScoped<IContentTypePartDefinitionDisplayDriver, WizardPartSettingsDisplayDriver>();

        services.AddDataMigration<WizardPartMigrations>();

        services.AddScoped<IWizardHandler, ContentWizardHandler>();
        services.AddScoped<IWizardAccessPolicy, ContentWizardAccessPolicy>();
        services.AddScoped<IDisplayDriver<WizardFlow>, ContentWizardStepDisplayDriver>();
        services.AddSingleton<IWizardDefinition, ContentWizardDefinition>();
    }
}
