using CrestApps.Core.AI;
using CrestApps.OrchardCore.Commerce.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Commerce;

/// <summary>
/// Registers the store setup AI tools whenever the Commerce and AI features are on. They need <c>SetUpStore</c>, as
/// Commerce › Set up your store does; running a step's quick action asks for approval, because some steps create a
/// content type or staff roles.
/// </summary>
[Feature(CommerceConstants.Features.Area)]
[RequireFeatures(AIFeatureIds.Area)]
public sealed class AIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIToolsStartup(IStringLocalizer<AIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<StoreSetupToolScope>();

        services.AddCoreAITool<GetStoreSetupGuideTool>(GetStoreSetupGuideTool.TheName)
            .WithTitle(S["Get Store Setup Guide"])
            .WithDescription(S["Lists the \"Set up your store\" steps in order with whether each is done, skipped or optional, what is set up now, the admin page that does it in full, and the fields its quick action takes. Use it when asked what is left before the store can sell, and before running a step."])
            .WithCategory(S["Commerce"])
            .Selectable();

        services.AddCoreAITool<RunStoreSetupStepTool>(RunStoreSetupStepTool.TheName)
            .WithTitle(S["Run Store Setup Step"])
            .WithDescription(S["Runs one setup step's quick action with the given field values (for example add the Product type, choose the store currency, save the store details, add a flat shipping rate, add the staff roles) and marks the step done. Only steps that report a quick action can be run; some of them create a content type or roles."])
            .WithCategory(S["Commerce"])
            .AsSecurity()
            .Selectable();
    }
}
