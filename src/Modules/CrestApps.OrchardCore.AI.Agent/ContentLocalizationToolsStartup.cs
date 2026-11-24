using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Agent.ContentLocalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Registers the tool that creates a translation of a content item, when the Content
/// Localization feature is on.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.ContentLocalization)]
public sealed class ContentLocalizationToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentLocalizationToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ContentLocalizationToolsStartup(IStringLocalizer<ContentLocalizationToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<LocalizeContentItemTool>(LocalizeContentItemTool.TheName)
            .WithTitle(S["Localize Content Item"])
            .WithDescription(S["Creates the version of a content item in another culture as a draft copy linked to the same localization set, and returns the new content item ID. Fails if that culture already exists or the type has no localization part; translate the copy with createOrUpdateContentItem afterwards."])
            .WithCategory(S["Content Management"])
            .Selectable();
    }
}
