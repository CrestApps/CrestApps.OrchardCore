using CrestApps.OrchardCore.Tickets.Core.Services;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.YesSql.Core.Upgrades;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.ContentManagement.Metadata;

namespace CrestApps.OrchardCore.Tickets.Core.Handlers;

/// <summary>
/// When a taxonomy that holds a ticket type's categories is published with a category under another parent (or with
/// another key), asks for <see cref="TicketCategoryPathsStep"/>, which brings the tickets' category paths up to date. A
/// rename or a change of routing changes no path, so it asks for nothing.
/// </summary>
public sealed class TicketCategoryTaxonomyHandler : ContentHandlerBase
{
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly ResumableSteps _steps;

    /// <summary>
    /// Initializes a new instance of the <see cref="TicketCategoryTaxonomyHandler"/> class.
    /// </summary>
    public TicketCategoryTaxonomyHandler(IContentDefinitionManager contentDefinitionManager, ResumableSteps steps)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _steps = steps;
    }

    /// <inheritdoc/>
    public override async Task PublishedAsync(PublishContentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.PreviousItem is null
            || !PathsChanged(context.PreviousItem, context.ContentItem)
            || !await IsCategoryTaxonomyAsync(context.ContentItem.ContentItemId))
        {
            return;
        }

        await TicketCategoryPathsStep.RequestAsync(_steps);
    }

    /// <summary>
    /// Gets whether a category of a taxonomy has another path than before. Pure.
    /// </summary>
    /// <param name="previous">The taxonomy as it was.</param>
    /// <param name="current">The taxonomy as it is now.</param>
    /// <returns><see langword="true"/> when a category that was there before has another path, or one is gone.</returns>
    public static bool PathsChanged(ContentItem previous, ContentItem current)
    {
        var before = TicketCategoryService.Read(previous).ToDictionary(category => category.Id, category => category.Path, StringComparer.Ordinal);
        var after = TicketCategoryService.Read(current).ToDictionary(category => category.Id, category => category.Path, StringComparer.Ordinal);

        return before.Any(category => !after.TryGetValue(category.Key, out var path) || !string.Equals(path, category.Value, StringComparison.Ordinal));
    }

    private async Task<bool> IsCategoryTaxonomyAsync(string taxonomyId)
    {
        foreach (var type in await _contentDefinitionManager.ListTypeDefinitionsAsync())
        {
            if (type.Parts.Any(part => part.PartDefinition.Name == TicketsConstants.PartName
                && string.Equals(part.GetSettings<TicketPartSettings>().CategoryTaxonomyId, taxonomyId, StringComparison.Ordinal)))
            {
                return true;
            }
        }

        return false;
    }
}
