using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Agent.DataLocalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Registers the tools that list and save dynamic translations, when the Data Localization feature is on.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.DataLocalization)]
public sealed class DataLocalizationToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="DataLocalizationToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DataLocalizationToolsStartup(IStringLocalizer<DataLocalizationToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<DynamicTranslationScopeChanges>();

        services.AddCoreAITool<ListDynamicTranslationsTool>(ListDynamicTranslationsTool.TheName)
            .WithTitle(S["List Dynamic Translations"])
            .WithDescription(S["Lists the translatable dynamic strings for one culture with their current translation, filterable by context, search text and \"untranslated only\", and returns translation progress per culture. Use it to find what is missing before translating."])
            .WithCategory(S["Localization"])
            .Selectable();

        services.AddCoreAITool<SaveDynamicTranslationsTool>(SaveDynamicTranslationsTool.TheName)
            .WithTitle(S["Save Dynamic Translations"])
            .WithDescription(S["Adds or changes translations of dynamic strings for one culture; strings not sent keep their current translation. The culture must be one of the site's supported cultures."])
            .WithCategory(S["Localization"])
            .Selectable();
    }
}
