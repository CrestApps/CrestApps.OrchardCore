using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Conditions;
using CrestApps.OrchardCore.ContentFields.Conditions;
using CrestApps.OrchardCore.ContentFields.Taxonomies;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.ContentFields.ConditionalFields;

/// <summary>
/// Reads a taxonomy field: the chosen terms, with the terms above them, so a rule on a category also covers its
/// sub-categories (<see cref="ConditionOperator.IsOrUnder"/>).
/// </summary>
internal sealed class TaxonomyFieldValueReader : IContentFieldValueReader
{
    private readonly IContentManager _contentManager;
    private readonly Dictionary<string, TaxonomyTermTree> _trees = new(StringComparer.Ordinal);

    public TaxonomyFieldValueReader(IContentManager contentManager)
    {
        _contentManager = contentManager;
    }

    public string FieldType => "TaxonomyField";

    public async Task<ConditionValue> ReadAsync(JsonNode field, ContentPartFieldDefinition definition)
    {
        var terms = JsonFieldValueReader.ToStrings((field as JsonObject)?["TermContentItemIds"]).ToArray();

        if (terms.Length == 0)
        {
            return ConditionValue.Empty;
        }

        var tree = await GetTreeAsync(definition);

        return new ConditionValue(terms, terms.SelectMany(term => tree.Find(term)?.AncestorIds ?? []).Distinct(StringComparer.Ordinal));
    }

    public async Task DescribeAsync(ContentPartFieldDefinition definition, ContentConditionSourceDescriptor descriptor)
    {
        var tree = await GetTreeAsync(definition);

        descriptor.IsHierarchical = true;

        foreach (var term in tree.Nodes)
        {
            descriptor.Options.Add(new ContentConditionOption(term.Id, term.Name, term.Level));

            if (term.AncestorIds.Count > 0)
            {
                descriptor.Ancestors[term.Id] = [.. term.AncestorIds];
            }
        }
    }

    private async Task<TaxonomyTermTree> GetTreeAsync(ContentPartFieldDefinition definition)
    {
        var taxonomyId = definition.Settings?["TaxonomyFieldSettings"]?["TaxonomyContentItemId"]?.GetValue<string>();

        if (string.IsNullOrEmpty(taxonomyId))
        {
            return TaxonomyTermTree.From(null);
        }

        if (!_trees.TryGetValue(taxonomyId, out var tree))
        {
            tree = TaxonomyTermTree.From(await _contentManager.GetAsync(taxonomyId, VersionOptions.Published));
            _trees[taxonomyId] = tree;
        }

        return tree;
    }
}
