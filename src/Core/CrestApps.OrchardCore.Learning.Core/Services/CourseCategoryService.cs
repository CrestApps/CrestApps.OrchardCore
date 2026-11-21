using System.Text.Json.Nodes;
using CrestApps.OrchardCore.ContentFields.Taxonomies;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Taxonomies.Settings;

namespace CrestApps.OrchardCore.Learning.Core.Services;

/// <summary>
/// The category field of courses: which field of the Course type's own part it is and the taxonomy it uses.
/// </summary>
/// <param name="PartName">The name of the part that holds the field, the Course type's own part.</param>
/// <param name="FieldName">The field's name.</param>
/// <param name="TaxonomyId">The content item id of the taxonomy.</param>
public sealed record CourseCategoryField(string PartName, string FieldName, string TaxonomyId);

/// <summary>
/// The course categories: the terms of the taxonomy the Course type's category field uses (the "Course categories"
/// taxonomy the Course Categories feature creates, or the one a site already had). Nothing here needs the feature: a
/// site whose courses have no taxonomy field simply has no categories.
/// </summary>
public sealed class CourseCategoryService
{
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly IContentManager _contentManager;

    private CourseCategoryField _field;
    private bool _fieldRead;
    private TaxonomyTermTree _tree;

    /// <summary>
    /// Initializes a new instance of the <see cref="CourseCategoryService"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    /// <param name="contentManager">The content manager.</param>
    public CourseCategoryService(IContentDefinitionManager contentDefinitionManager, IContentManager contentManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _contentManager = contentManager;
    }

    /// <summary>
    /// Gets the category field of the Course type, or <see langword="null"/> when courses have no taxonomy field with a
    /// taxonomy.
    /// </summary>
    /// <returns>The field.</returns>
    public async Task<CourseCategoryField> GetFieldAsync()
    {
        if (_fieldRead)
        {
            return _field;
        }

        _fieldRead = true;

        var type = await _contentDefinitionManager.GetTypeDefinitionAsync(LearningConstants.ContentTypes.Course);
        var part = type?.Parts.FirstOrDefault(candidate => candidate.Name == LearningConstants.ContentTypes.Course);

        if (part is null)
        {
            return null;
        }

        var candidates = part.PartDefinition.Fields
            .Select(field => new CourseCategoryFieldCandidate(
                field.Name,
                field.FieldDefinition.Name,
                field.FieldDefinition.Name == CourseCategorySetup.TaxonomyFieldType ? field.GetSettings<TaxonomyFieldSettings>()?.TaxonomyContentItemId : null))
            .ToList();
        var chosen = CourseCategorySetup.Pick(candidates);

        return _field = chosen is null || string.IsNullOrEmpty(chosen.TaxonomyId)
            ? null
            : new CourseCategoryField(part.Name, chosen.Name, chosen.TaxonomyId);
    }

    /// <summary>
    /// Gets the taxonomy of the category field, or <see langword="null"/> when courses have none.
    /// </summary>
    /// <returns>The taxonomy's content item id.</returns>
    public async Task<string> GetTaxonomyIdAsync()
        => (await GetFieldAsync())?.TaxonomyId;

    /// <summary>
    /// Gets the categories as the published taxonomy has them, in its order; empty when there is none.
    /// </summary>
    /// <returns>The categories.</returns>
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
    /// Finds a category by its name, or by its path of names from the top, and adds what is missing to the taxonomy,
    /// which is then published. One name is looked for anywhere in the tree.
    /// </summary>
    /// <param name="names">The name, or the path of names.</param>
    /// <returns>The category's id, or <see langword="null"/> when courses have no category field or no name was given.</returns>
    public async Task<string> FindOrAddAsync(IReadOnlyList<string> names)
    {
        var taxonomyId = await GetTaxonomyIdAsync();

        if (taxonomyId is null)
        {
            return null;
        }

        var latest = await _contentManager.GetAsync(taxonomyId, VersionOptions.Latest);

        if (latest is null)
        {
            return null;
        }

        var termType = ((JsonObject)latest.Content)["TaxonomyPart"]?["TermContentType"]?.GetValue<string>();
        termType = string.IsNullOrEmpty(termType) ? LearningConstants.CourseCategories.TermContentType : termType;

        var id = CourseCategoryTerms.FindOrAdd(latest, names, termType, out var added);

        if (!added)
        {
            return id;
        }

        // A new term is published with the taxonomy, so its page and its route exist as for a term added by hand.
        var draft = await _contentManager.GetAsync(taxonomyId, VersionOptions.DraftRequired);
        id = CourseCategoryTerms.FindOrAdd(draft, names, termType, out _);
        await _contentManager.PublishAsync(draft);
        _tree = null;

        return id;
    }

    /// <summary>
    /// Gets the categories of a course.
    /// </summary>
    /// <param name="course">The course.</param>
    /// <returns>The category ids; empty when the course has none or courses have no category field.</returns>
    public async Task<IReadOnlyList<string>> GetCategoryIdsAsync(ContentItem course)
    {
        ArgumentNullException.ThrowIfNull(course);

        if (await GetFieldAsync() is not { } field ||
            ((JsonObject)course.Content)[field.PartName]?[field.FieldName]?["TermContentItemIds"] is not JsonArray ids)
        {
            return [];
        }

        return [.. ids.Select(id => id?.GetValue<string>()).Where(id => !string.IsNullOrEmpty(id))];
    }

    /// <summary>
    /// Sets the categories of a course, replacing the ones it had. Nothing changes when courses have no category field.
    /// </summary>
    /// <param name="course">The course.</param>
    /// <param name="categoryIds">The category ids.</param>
    /// <returns>A task.</returns>
    public async Task SetCategoriesAsync(ContentItem course, IEnumerable<string> categoryIds)
    {
        ArgumentNullException.ThrowIfNull(course);

        if (await GetFieldAsync() is not { } field)
        {
            return;
        }

        var content = (JsonObject)course.Content;

        if (content[field.PartName] is not JsonObject part)
        {
            part = [];
            content[field.PartName] = part;
        }

        part[field.FieldName] = new JsonObject
        {
            ["TaxonomyContentItemId"] = field.TaxonomyId,
            ["TermContentItemIds"] = new JsonArray([.. (categoryIds ?? []).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).Select(id => (JsonNode)id)]),
        };
    }
}
