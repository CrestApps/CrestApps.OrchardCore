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
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentTypes.Editors;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
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
        services.AddScoped<IWizardSessionStore, WizardSessionStore>();
        services.AddScoped<IWizardEngine, DefaultWizardEngine>();
        services.AddScoped<IWizardDefinitionProvider, DefaultWizardDefinitionProvider>();
        services.AddScoped<WizardResumeCookieManager>();
        services.AddScoped<IDisplayDriver<WizardFlow>, DefaultWizardFlowDisplayDriver>();
        services.AddPermissionProvider<WizardPermissionsProvider>();

        services.AddDataMigration<WizardMigrations>()
            .AddIndexProvider<WizardSessionIndexProvider>();
    }
}

[RequireFeatures("OrchardCore.Workflows")]
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

[RequireFeatures("OrchardCore.Contents")]
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
