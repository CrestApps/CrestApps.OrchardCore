using CrestApps.OrchardCore.ContentFields.Taxonomies;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Taxonomies.Settings;

namespace CrestApps.OrchardCore.KnowledgeBase.Services;

/// <summary>
/// The categories of the help center: the terms of the taxonomy the Knowledge Article type's category field uses (the
/// Knowledge Base Categories taxonomy the feature creates, or another one an administrator picked).
/// </summary>
public sealed class KnowledgeBaseCategories
{
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly IContentManager _contentManager;

    private TaxonomyTermTree _tree;

    /// <summary>
    /// Initializes a new instance of the <see cref="KnowledgeBaseCategories"/> class.
    /// </summary>
    public KnowledgeBaseCategories(IContentDefinitionManager contentDefinitionManager, IContentManager contentManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _contentManager = contentManager;
    }

    /// <summary>
    /// Gets the taxonomy the Knowledge Article type's category field uses, or <see langword="null"/> when it has none.
    /// </summary>
    /// <returns>The taxonomy's content item id.</returns>
    public async Task<string> GetTaxonomyIdAsync()
    {
        var type = await _contentDefinitionManager.GetTypeDefinitionAsync(KnowledgeBaseConstants.ArticleType);
        var field = type?.Parts
            .FirstOrDefault(part => part.Name == KnowledgeBaseConstants.ArticleType)?
            .PartDefinition.Fields
            .FirstOrDefault(value => value.Name == KnowledgeBaseConstants.CategoryField);

        var taxonomyId = field?.GetSettings<TaxonomyFieldSettings>()?.TaxonomyContentItemId;

        return string.IsNullOrEmpty(taxonomyId) ? null : taxonomyId;
    }

    /// <summary>
    /// Gets the categories, as the published taxonomy has them, in its order; empty when there is no taxonomy.
    /// </summary>
    /// <returns>The terms.</returns>
    public async Task<TaxonomyTermTree> GetTreeAsync()
    {
        if (_tree is not null)
        {
            return _tree;
        }

        var taxonomyId = await GetTaxonomyIdAsync();
        var taxonomy = taxonomyId is null ? null : await _contentManager.GetAsync(taxonomyId, VersionOptions.Published);

        return _tree = TaxonomyTermTree.From(taxonomy);
    }

    /// <summary>
    /// Gets a category and the categories under it, so choosing a category lists the articles of its sub-categories too.
    /// </summary>
    /// <param name="tree">The categories.</param>
    /// <param name="categoryId">The category.</param>
    /// <returns>The ids; empty when the category doesn't exist.</returns>
    public static IReadOnlyList<string> WithDescendants(TaxonomyTermTree tree, string categoryId)
    {
        ArgumentNullException.ThrowIfNull(tree);

        if (tree.Find(categoryId) is null)
        {
            return [];
        }

        return [.. tree.Nodes.Where(node => node.Id == categoryId || node.AncestorIds.Contains(categoryId, StringComparer.Ordinal)).Select(node => node.Id)];
    }
}
