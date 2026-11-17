using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Agent.Contents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Registers the content item tools that list items and versions, restore a version, discard a draft and change the
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

        services.AddCoreAITool<RestoreContentItemVersionTool>(RestoreContentItemVersionTool.TheName)
            .WithTitle(S["Restore Content Item Version"])
            .WithDescription(S["Restores an earlier version of a content item as a new draft, keeping the published version live. Takes a content item version ID; the current draft, if any, stops being the latest. Publish the item afterwards to make it live."])
            .WithCategory(S["Content Management"])
            .Selectable();

        services.AddCoreAITool<DiscardContentItemDraftTool>(DiscardContentItemDraftTool.TheName)
            .WithTitle(S["Discard Content Item Draft"])
            .WithDescription(S["Discards the unpublished draft of a content item so the published version becomes the latest again. Fails when the latest version is published; when the item was never published the item disappears from the lists."])
            .WithCategory(S["Content Management"])
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<ChangeContentItemOwnerTool>(ChangeContentItemOwnerTool.TheName)
            .WithTitle(S["Change Content Item Owner"])
            .WithDescription(S["Changes the owner of a content item to another user, given by user name, user ID or email. Ownership decides who can edit, publish or delete the item under the \"own content\" permissions."])
            .WithCategory(S["Content Management"])
            .RequireApproval()
            .Selectable();
    }
}
