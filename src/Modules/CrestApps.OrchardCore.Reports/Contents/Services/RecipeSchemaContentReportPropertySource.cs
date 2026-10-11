using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas;
using CrestApps.OrchardCore.Reports.Contents.Models;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Queries;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Reports.Contents.Services;

/// <summary>
/// Describes the properties of parts from the JSON schemas the CrestApps Recipes feature keeps for them, so a part
/// written in code has typed columns even before any content item stores it. Registered only when that feature is on.
/// </summary>
public sealed class RecipeSchemaContentReportPropertySource : IContentReportPropertySource
{
    private readonly IEnumerable<IContentSchemaDefinition> _schemaDefinitions;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecipeSchemaContentReportPropertySource"/> class.
    /// </summary>
    /// <param name="schemaDefinitions">The content schemas the Recipes feature registers.</param>
    public RecipeSchemaContentReportPropertySource(IEnumerable<IContentSchemaDefinition> schemaDefinitions)
    {
        _schemaDefinitions = schemaDefinitions;
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<ContentReportProperty>> GetPropertiesAsync(ContentTypePartDefinition typePart, CancellationToken cancellationToken = default)
    {
        var partName = typePart?.PartDefinition?.Name;

        if (string.IsNullOrEmpty(partName))
        {
            return [];
        }

        var properties = new List<ContentReportProperty>();

        foreach (var definition in _schemaDefinitions
            .OfType<IContentPartSchemaDefinition>()
            .Where(definition => string.Equals(definition.Name, partName, StringComparison.OrdinalIgnoreCase)))
        {
            var builder = await definition.GetPartSchemaAsync(new ContentPartSchemaContext { ContentTypePartDefinition = typePart }, cancellationToken);

            if (builder is not null)
            {
                Collect(JsonSerializer.SerializeToNode(builder.Build()) as JsonObject, null, 1, properties);
            }
        }

        return properties;
    }

    // Walks the "properties" of an object schema: a plain value is a column, an object is walked into, and a list is a
    // column when it holds plain values.
    private static void Collect(JsonObject schema, string prefix, int depth, List<ContentReportProperty> properties)
    {
        if (schema?["properties"] is not JsonObject members)
        {
            return;
        }

        foreach (var (name, member) in members)
        {
            if (member is not JsonObject memberSchema)
            {
                continue;
            }

            var path = prefix is null ? name : prefix + "." + name;
            var types = Types(memberSchema);

            if (types.Contains("object"))
            {
                if (depth < QueryResultSchema.MaxDepth)
                {
                    Collect(memberSchema, path, depth + 1, properties);
                }

                continue;
            }

            if (types.Contains("array"))
            {
                var itemTypes = Types(memberSchema["items"] as JsonObject);

                if (itemTypes.Count > 0 && !itemTypes.Contains("object") && !itemTypes.Contains("array"))
                {
                    properties.Add(new ContentReportProperty(path, ReportDataType.Text));
                }

                continue;
            }

            var dataType = DataType(types, memberSchema["format"]?.GetValue<string>());

            if (dataType is not null)
            {
                properties.Add(new ContentReportProperty(path, dataType.Value));
            }
        }
    }

    private static HashSet<string> Types(JsonObject schema)
    {
        var types = new HashSet<string>(StringComparer.Ordinal);

        switch (schema?["type"])
        {
            case JsonValue value when value.TryGetValue<string>(out var type):
                types.Add(type);

                break;

            case JsonArray array:
                foreach (var item in array)
                {
                    if (item is JsonValue entry && entry.TryGetValue<string>(out var type) && type != "null")
                    {
                        types.Add(type);
                    }
                }

                break;
        }

        return types;
    }

    private static ReportDataType? DataType(HashSet<string> types, string format)
    {
        if (types.Contains("string"))
        {
            return format switch
            {
                "date-time" => ReportDataType.DateTime,
                "date" => ReportDataType.Date,
                _ => ReportDataType.Text,
            };
        }

        if (types.Contains("integer"))
        {
            return ReportDataType.Integer;
        }

        if (types.Contains("number"))
        {
            return ReportDataType.Decimal;
        }

        if (types.Contains("boolean"))
        {
            return ReportDataType.Boolean;
        }

        return null;
    }
}
