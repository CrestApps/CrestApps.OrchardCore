using CrestApps.Core.AI;
using CrestApps.OrchardCore.KnowledgeBase.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.KnowledgeBase;

/// <summary>
/// Registers the Knowledge Base AI tools, which administrators can pick on AI profiles, whenever the AI feature is on:
/// search the help articles and read one. Both act as the signed-in user, return only published articles that user may
/// open, change nothing and ask for no approval.
/// </summary>
[Feature(KnowledgeBaseConstants.Feature)]
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
        services.AddCoreAITool<SearchKnowledgeBaseTool>(SearchKnowledgeBaseTool.TheName)
            .WithTitle(S["Search knowledge base"])
            .WithDescription(S["Finds the published help articles the user may read, by keywords or a question, with their link and the passage that matches."])
            .WithCategory(S["Knowledge Base"])
            .Selectable();

        services.AddCoreAITool<GetKnowledgeArticleTool>(GetKnowledgeArticleTool.TheName)
            .WithTitle(S["Get knowledge article"])
            .WithDescription(S["Reads one published help article the user may read, in full."])
            .WithCategory(S["Knowledge Base"])
            .Selectable();
    }
}
