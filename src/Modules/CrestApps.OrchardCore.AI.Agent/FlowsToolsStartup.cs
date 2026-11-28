using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Agent.Contents;
using CrestApps.OrchardCore.AI.Agent.Flows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Registers the tools that save and remove the content items nested in Flow, Bag and widgets list parts, when the
/// Flows feature is on.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Flows)]
public sealed class FlowsToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="FlowsToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public FlowsToolsStartup(IStringLocalizer<FlowsToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        // The nested item tools prepare and check a nested item the way createOrUpdateContentItem does. The Flows
        // feature can be on without the Contents feature, so the helpers are registered here too.
        services.TryAddScoped<IContentItemPayloadAssistanceService, DefaultContentItemPayloadAssistanceService>();
        services.TryAddScoped<ContentItemPreparationService>();

        services.AddCoreAITool<SaveNestedContentItemTool>(SaveNestedContentItemTool.TheName)
            .WithTitle(S["Save Nested Content Item"])
            .WithDescription(S["Adds, updates or moves a single widget or contained item inside a content item's Flow, Bag or widget-zone part without resending the rest of the page. Takes the parent item, the target part (and zone or parent nested item), an optional afterId (the sibling it goes after), and the nested item's content."])
            .WithCategory(S["Content Management"])
            .Selectable();

        services.AddCoreAITool<RemoveNestedContentItemTool>(RemoveNestedContentItemTool.TheName)
            .WithTitle(S["Remove Nested Content Item"])
            .WithDescription(S["Deletes one widget or contained item, and everything nested inside it, from a content item's Flow, Bag or widget-zone part."])
            .WithCategory(S["Content Management"])
            .AsDestructive()
            .Selectable();
    }
}
