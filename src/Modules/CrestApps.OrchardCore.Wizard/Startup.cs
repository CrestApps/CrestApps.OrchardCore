using CrestApps.OrchardCore.Wizard.Core.Indexes;
using CrestApps.OrchardCore.Wizard.Core.Migrations;
using CrestApps.OrchardCore.Wizard.Core.Services;
using CrestApps.OrchardCore.Wizard.Drivers;
using CrestApps.OrchardCore.Wizard.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

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
