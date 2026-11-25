using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Agent.Contents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Registers the content item tools that get one item, list items and versions, compare and restore versions, discard a draft and change the
/// owner, when the Contents feature is on.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Contents)]
public sealed class ContentsToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentsToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ContentsToolsStartup(IStringLocalizer<ContentsToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<GetContentItemTool>(GetContentItemTool.TheName)
            .WithTitle(S["Get Content Item"])
            .WithDescription(S["Gets one content item by its ID or by a URL, path, alias or handle (slug:about-us, alias:main-menu), and returns its ID, type, title and state; a path of / returns the home page. Add include localizations for its translation in each site culture, and nested for the outline of the widgets and items in its Flow, Bag and widget-zone parts."])
            .WithCategory(S["Content Management"])
            .Selectable();

        services.AddCoreAITool<ListContentItemsTool>(ListContentItemsTool.TheName)
            .WithTitle(S["List Content Items"])
            .WithDescription(S["Lists content items with the same filters as the admin Content Items page: text, content types, stereotype, status (latest, published, draft, owned by me, all versions), sort, culture, and paging. Use it to find items and their IDs; it returns summaries, not full bodies."])
            .WithCategory(S["Content Management"])
            .Selectable();

        services.AddCoreAITool<ListContentItemVersionsTool>(ListContentItemVersionsTool.TheName)
            .WithTitle(S["List Content Item Versions"])
            .WithDescription(S["Lists the stored versions of one content item (version ID, created/modified/published dates, author, latest and published flags). Use it before restoring a version."])
            .WithCategory(S["Content Management"])
            .Selectable();

        services.AddCoreAITool<CompareContentItemVersionsTool>(CompareContentItemVersionsTool.TheName)
            .WithTitle(S["Compare Content Item Versions"])
            .WithDescription(S["Shows what changed between two versions of a content item, field by field, with each value before and after; by default the draft against the published version, or the latest version against the one before it. Use it for \"what changed on this page?\" and before restoreContentItemVersion. Needs the right to edit the item."])
            .WithCategory(S["Content Management"])
            .Selectable();

        services.AddCoreAITool<RestoreContentItemVersionTool>(RestoreContentItemVersionTool.TheName)
            .WithTitle(S["Restore Content Item Version"])
            .WithDescription(S["Restores an earlier version of a content item as a new draft, keeping the published version live. Takes a content item version ID; the current draft, if any, stops being the latest. Publish the item afterwards to make it live."])
            .WithCategory(S["Content Management"])
            .Selectable();

        services.AddCoreAITool<DiscardContentItemDraftTool>(DiscardContentItemDraftTool.TheName)
            .WithTitle(S["Discard Content Item Draft"])
            .WithDescription(S["Discards the unpublished draft of a content item so the published version becomes the latest again. Fails when the latest version is published; when the item was never published the item disappears from the lists."])
            .WithCategory(S["Content Management"])
            .AsDestructive()
            .Selectable();

        services.AddCoreAITool<ChangeContentItemOwnerTool>(ChangeContentItemOwnerTool.TheName)
            .WithTitle(S["Change Content Item Owner"])
            .WithDescription(S["Changes the owner of a content item to another user, given by user name, user ID or email. Ownership decides who can edit, publish or delete the item under the \"own content\" permissions."])
            .WithCategory(S["Content Management"])
            .AsSecurity()
            .Selectable();
    }
}
