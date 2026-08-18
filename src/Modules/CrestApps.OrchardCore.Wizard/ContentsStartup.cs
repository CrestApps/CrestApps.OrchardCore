using CrestApps.OrchardCore.Wizard.Contents;
using CrestApps.OrchardCore.Wizard.Drivers;
using CrestApps.OrchardCore.Wizard.Handlers;
using CrestApps.OrchardCore.Wizard.Migrations;
using CrestApps.OrchardCore.Wizard.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentTypes.Editors;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Wizard;

/// <summary>
/// Registers the content-driven wizard feature: the wizard part, its settings and authoring editor, and the
/// runtime services that turn a content item into a wizard.
/// </summary>
[Feature(WizardConstants.Features.Contents)]
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
