using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Documents.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Documents;

/// <summary>
/// Registers the AI tool that lists an AI profile's documents, when the AI Documents for Profiles feature is on.
/// </summary>
[Feature(AIConstants.Feature.ProfileDocuments)]
[RequireFeatures(AIConstants.Feature.Area)]
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
        services.AddCoreAITool<ListAIProfileDocumentsTool>(ListAIProfileDocumentsTool.TheName)
            .WithTitle(S["List AI Profile Documents"])
            .WithDescription(S["Lists the documents an AI profile answers from, with file name, type and size. Use it to see what knowledge a profile has before adding or removing a document."])
            .WithCategory(S["AI Knowledge"])
            .Selectable();
    }
}
