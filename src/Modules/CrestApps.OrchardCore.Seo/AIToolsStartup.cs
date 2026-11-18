using CrestApps.Core.AI;
using CrestApps.OrchardCore.Seo.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Seo;

/// <summary>
/// Registers the structured data AI tools added after the first three (see <see cref="AIStartup"/>) whenever the AI
/// feature is on. <c>getContentItemStructuredData</c> needs <c>ViewContent</c> on the published item.
/// </summary>
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
        services.AddCoreAITool<GetContentItemStructuredDataTool>(GetContentItemStructuredDataTool.TheName)
            .WithTitle(S["Get Content Item Structured Data"])
            .WithDescription(S["Returns the JSON-LD a content item's page publishes, built by the same engine as the page, or says that its content type publishes none. Use it to check the result after configuring schema.org or to answer \"what do search engines see for this item\"."])
            .WithCategory(S["Search Engine Optimization"])
            .Selectable();
    }
}
