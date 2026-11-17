using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Agent.Personas;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Registers the tools that list, save and delete AI personas, when the AI Personas feature is on. A persona the
/// running assistant speaks as cannot be changed or deleted from a chat.
/// </summary>
[RequireFeatures(AIConstants.Feature.Personas)]
public sealed class AIPersonasToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIPersonasToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIPersonasToolsStartup(IStringLocalizer<AIPersonasToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListAIPersonasTool>(ListAIPersonasTool.TheName)
            .WithTitle(S["List AI Personas"])
            .WithDescription(S["Lists personas with name, title, assistant name, language, voice and the profiles that link to each. Pass name to also get that persona's full instructions."])
            .WithCategory(S["AI Personas"])
            .Selectable();

        services.AddCoreAITool<SaveAIPersonaTool>(SaveAIPersonaTool.TheName)
            .WithTitle(S["Save AI Persona"])
            .WithDescription(S["Creates a persona or updates one by id. The change reaches every linked profile from its next message, so a persona linked to the running assistant cannot be changed. The technical name cannot change."])
            .WithCategory(S["AI Personas"])
            .Selectable();

        services.AddCoreAITool<DeleteAIPersonaTool>(DeleteAIPersonaTool.TheName)
            .WithTitle(S["Delete AI Personas"])
            .WithDescription(S["Deletes personas by id. Profiles that linked to a deleted persona then run without a persona; the result says how many."])
            .WithCategory(S["AI Personas"])
            .RequireApproval()
            .Selectable();
    }
}
