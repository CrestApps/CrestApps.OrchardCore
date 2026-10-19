using System.Data;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Taxonomies.Fields;
using OrchardCore.Taxonomies.Settings;

namespace CrestApps.OrchardCore.ContentTransfer.Handlers.Fields;

/// <summary>
/// Imports and exports a taxonomy field: its terms by name (a comma-separated list, where "Clothing > Shirts" names the
/// last part), or by content item id. A name the taxonomy doesn't have is left out.
/// </summary>
public sealed class TaxonomyFieldImportHandler : ContentImportHandlerBase, IContentFieldImportHandler
{
    private readonly IContentManager _contentManager;
    private readonly Dictionary<string, IReadOnlyList<(string Id, string Name)>> _terms = new(StringComparer.Ordinal);

    internal readonly IStringLocalizer S;

    public TaxonomyFieldImportHandler(IContentManager contentManager, IStringLocalizer<TaxonomyFieldImportHandler> stringLocalizer)
    {
        _contentManager = contentManager;
        S = stringLocalizer;
    }

    public IReadOnlyCollection<ImportColumn> GetColumns(ImportContentFieldContext context)
    {
        var prefix = $"{context.PartName}_{context.ContentPartFieldDefinition.Name}_";
        var displayName = context.ContentPartFieldDefinition.DisplayName();

        return
        [
            new ImportColumn()
            {
                Name = $"{prefix}Terms",
                Description = S["A comma-separated list of the names of the terms for {0}. A name the taxonomy doesn't have is left out.", displayName],
                AdditionalNames = [displayName, context.ContentPartFieldDefinition.Name],
            },
            new ImportColumn()
            {
                Name = $"{prefix}{nameof(TaxonomyField.TaxonomyContentItemId)}",
                Description = S["The taxonomy content item id for {0}", displayName],
                Type = ImportColumnType.ExportOnly,
            },
            new ImportColumn()
            {
                Name = $"{prefix}{nameof(TaxonomyField.TermContentItemIds)}",
                Description = S["A comma-separated list of taxonomy term content item ids for {0}", displayName],
            },
        ];
    }

    public async Task ImportAsync(ContentFieldImportMapContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var taxonomyId = context.ContentPartFieldDefinition.GetSettings<TaxonomyFieldSettings>()?.TaxonomyContentItemId;

        if (string.IsNullOrEmpty(taxonomyId) || context.Columns is null || context.Row is null)
        {
            return;
        }

        var columns = GetColumns(context).ToArray();
        var terms = await GetTermsAsync(taxonomyId);
        string[] chosen = null;

        foreach (DataColumn column in context.Columns)
        {
            if (Is(column.ColumnName, columns[2]))
            {
                var ids = SplitCellValues(context.Row, column);

                if (ids.Length > 0)
                {
                    chosen = [.. ids.Where(id => terms.Any(term => term.Id == id))];
                }
            }
            else if (Is(column.ColumnName, columns[0]) && chosen is null)
            {
                var names = SplitCellValues(context.Row, column);

                if (names.Length > 0)
                {
                    chosen = [.. names
                        .Select(name => name.Split('>').Last().Trim())
                        .Select(name => terms.FirstOrDefault(term => string.Equals(term.Name, name, StringComparison.OrdinalIgnoreCase)).Id)
                        .Where(id => id is not null)
                        .Distinct(StringComparer.Ordinal)];
                }
            }
        }

        // A row that names no terms leaves the field as it was.
        if (chosen is null)
        {
            return;
        }

        if (context.ContentPartFieldDefinition.GetSettings<TaxonomyFieldSettings>()?.Unique == true)
        {
            chosen = [.. chosen.Take(1)];
        }

        context.ContentPart.Alter<TaxonomyField>(context.ContentPartFieldDefinition.Name, field =>
        {
            field.TaxonomyContentItemId = taxonomyId;
            field.TermContentItemIds = chosen;
        });
    }

    public async Task ExportAsync(ContentFieldExportMapContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.ContentPart);
        ArgumentNullException.ThrowIfNull(context.Row);

        var field = context.ContentPart.Get<TaxonomyField>(context.ContentPartFieldDefinition.Name);

        if (field == null)
        {
            return;
        }

        var columns = GetColumns(context).ToArray();
        var terms = string.IsNullOrEmpty(field.TaxonomyContentItemId) ? [] : await GetTermsAsync(field.TaxonomyContentItemId);

        context.Row[columns[0].Name] = string.Join(", ", (field.TermContentItemIds ?? [])
            .Select(id => terms.FirstOrDefault(term => term.Id == id).Name)
            .Where(name => !string.IsNullOrEmpty(name)));
        context.Row[columns[1].Name] = field.TaxonomyContentItemId ?? string.Empty;
        context.Row[columns[2].Name] = field.TermContentItemIds?.Length > 0 ? string.Join(",", field.TermContentItemIds) : string.Empty;
    }

    // Every term of the taxonomy, nested ones included, read once per import.
    private async Task<IReadOnlyList<(string Id, string Name)>> GetTermsAsync(string taxonomyId)
    {
        if (_terms.TryGetValue(taxonomyId, out var cached))
        {
            return cached;
        }

        var taxonomy = await _contentManager.GetAsync(taxonomyId, VersionOptions.Latest);
        var terms = new List<(string Id, string Name)>();

        void Walk(JsonNode node)
        {
            if (node is not JsonArray items)
            {
                return;
            }

            foreach (var item in items.OfType<JsonObject>())
            {
                if (item["ContentItemId"]?.GetValue<string>() is { Length: > 0 } id)
                {
                    terms.Add((id, item["DisplayText"]?.GetValue<string>()));
                }

                Walk(item["Terms"]);
            }
        }

        if (taxonomy is not null)
        {
            Walk(((JsonObject)taxonomy.Content)["TaxonomyPart"]?["Terms"]);
        }

        return _terms[taxonomyId] = terms;
    }
}
