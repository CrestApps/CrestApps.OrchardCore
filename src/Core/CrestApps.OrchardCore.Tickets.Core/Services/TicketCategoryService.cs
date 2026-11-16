using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.ContentFields.Taxonomies;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;

namespace CrestApps.OrchardCore.Tickets.Core.Services;

/// <summary>
/// Reads ticket categories from the published taxonomy a ticket type names. A category takes each routing value it leaves
/// empty from the nearest category above it that sets it, and a category hidden from customers hides those below it.
/// Taxonomies are read once per request.
/// </summary>
internal sealed class TicketCategoryService : ITicketCategoryService
{
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly IContentManager _contentManager;
    private readonly Dictionary<string, IReadOnlyList<TicketCategory>> _byTaxonomy = new(StringComparer.Ordinal);

    public TicketCategoryService(IContentDefinitionManager contentDefinitionManager, IContentManager contentManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _contentManager = contentManager;
    }

    public async Task<IReadOnlyList<TicketCategory>> ListAsync(string contentType, bool customersOnly = false, CancellationToken cancellationToken = default)
    {
        var all = await LoadAsync(await TaxonomyIdAsync(contentType));

        return customersOnly ? [.. all.Where(category => category.CustomersCanChoose)] : all;
    }

    public async Task<TicketCategory> FindAsync(string contentType, string categoryId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(categoryId))
        {
            return null;
        }

        return (await LoadAsync(await TaxonomyIdAsync(contentType))).FirstOrDefault(category => category.Id == categoryId);
    }

    /// <summary>
    /// Reads the categories of a taxonomy, with the routing each inherits. Pure.
    /// </summary>
    /// <param name="taxonomy">The taxonomy, or <see langword="null"/>.</param>
    /// <returns>The categories, in tree order.</returns>
    public static List<TicketCategory> Read(ContentItem taxonomy)
    {
        var tree = TaxonomyTermTree.From(taxonomy);
        var categories = new List<TicketCategory>();
        var byId = new Dictionary<string, TicketCategory>(StringComparer.Ordinal);

        // Tree order: a category's parent is always read before it.
        foreach (var node in tree.Nodes)
        {
            var own = Part(node.Content);
            var parent = node.ParentId is null ? null : byId.GetValueOrDefault(node.ParentId);
            var category = new TicketCategory
            {
                Id = node.Id,
                Name = node.Name,
                ParentId = node.ParentId,
                Level = node.Level,
                PathText = tree.GetPathText(node.Id),
                Path = TicketCategory.MakePath(node.Id, node.AncestorIds),
                HasChildren = tree.ChildrenOf(node.Id).Any(),
                DeskId = Pick(own?.DeskId, parent?.DeskId),
                TeamId = Pick(own?.TeamId, parent?.TeamId),
                PriorityId = Pick(own?.PriorityId, parent?.PriorityId),
                ServiceLevelPolicyId = Pick(own?.ServiceLevelPolicyId, parent?.ServiceLevelPolicyId),
                CustomersCanChoose = own?.HiddenFromCustomers != true && (parent?.CustomersCanChoose ?? true),
                CustomerDescription = own?.CustomerDescription,
            };

            categories.Add(category);
            byId[category.Id] = category;
        }

        return categories;
    }

    private async Task<string> TaxonomyIdAsync(string contentType)
    {
        if (string.IsNullOrEmpty(contentType))
        {
            return null;
        }

        var type = await _contentDefinitionManager.GetTypeDefinitionAsync(contentType);

        return type?.Parts.FirstOrDefault(part => part.PartDefinition.Name == TicketsConstants.PartName)?.GetSettings<TicketPartSettings>().CategoryTaxonomyId;
    }

    private async Task<IReadOnlyList<TicketCategory>> LoadAsync(string taxonomyId)
    {
        if (string.IsNullOrEmpty(taxonomyId))
        {
            return [];
        }

        if (!_byTaxonomy.TryGetValue(taxonomyId, out var categories))
        {
            categories = Read(await _contentManager.GetAsync(taxonomyId, VersionOptions.Published));
            _byTaxonomy[taxonomyId] = categories;
        }

        return categories;
    }

    private static TicketCategoryPart Part(JsonObject term)
        => term?[TicketsConstants.CategoryPartName] is JsonObject part ? part.Deserialize<TicketCategoryPart>() : null;

    private static string Pick(string own, string inherited)
        => string.IsNullOrWhiteSpace(own) ? inherited : own;
}
