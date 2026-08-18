using CrestApps.OrchardCore.Wizard.Handlers;
using CrestApps.OrchardCore.Wizard.Workflows.Drivers;
using CrestApps.OrchardCore.Wizard.Workflows.Handlers;
using CrestApps.OrchardCore.Wizard.Workflows.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Workflows.Helpers;

namespace CrestApps.OrchardCore.Wizard;

/// <summary>
/// Registers the wizard workflow activities and the handler that raises workflow events from the wizard
/// lifecycle.
/// </summary>
[Feature(WizardConstants.Features.Workflows)]
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
