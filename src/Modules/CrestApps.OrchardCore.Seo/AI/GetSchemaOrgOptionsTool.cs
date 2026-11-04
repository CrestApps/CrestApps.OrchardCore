using System.Text.Json;
using CrestApps.OrchardCore.Seo.Drivers;
using CrestApps.OrchardCore.Seo.SchemaOrg;
using CrestApps.OrchardCore.Seo.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement.Metadata;

namespace CrestApps.OrchardCore.Seo.AI;

/// <summary>
/// Tells an AI agent how the site's schema.org structured data can be configured: the schema.org types it knows and,
/// for a content type, its current settings, the values its items can give, the properties of a type and the
/// mappings the editor would suggest.
/// </summary>
public sealed class GetSchemaOrgOptionsTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "getSchemaOrgOptions";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "contentType": {
          "type": "string",
          "description": "The technical name of a content type, such as BlogPost. Leave it out to list the content types and the schema.org types."
        },
        "schemaType": {
          "type": "string",
          "description": "A schema.org type, such as BlogPosting or Event, to list its properties and the suggested mappings for the content type. Defaults to the type the content type uses."
        }
      }
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists how schema.org structured data (JSON-LD) can be configured on this site. Without a content type: the content types with their schema.org type, and the schema.org types the editor knows. With a content type: its schema.org settings, the values its items can give (fields, title, author, dates, the site's publisher), the properties of a schema.org type with whether search engines require them, and the suggested mappings. Call it before configureContentTypeSchemaOrg.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    /// <inheritdoc/>
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var contentType = SchemaOrgToolArguments.GetString(arguments, "contentType");
        var schemaType = SchemaOrgToolArguments.GetString(arguments, "schemaType");

        if (contentType is null)
        {
            var contentDefinitionManager = arguments.Services.GetRequiredService<IContentDefinitionManager>();
            var definitions = await contentDefinitionManager.ListTypeDefinitionsAsync();

            return JsonSerializer.Serialize(new
            {
                contentTypes = definitions.Select(definition => new
                {
                    definition.Name,
                    definition.DisplayName,
                    schemaType = SchemaOrgPartDefinitions.Find(definition)?.GetSettings<Models.SchemaOrgPartSettings>().SchemaType,
                    hasSchemaOrgPart = SchemaOrgPartDefinitions.Find(definition) is not null,
                }),
                schemaTypes = SchemaOrgVocabulary.Types
                    .Where(type => type.Group is not null)
                    .Select(type => new { type.Name, type.Group, type.Description }),
                note = "Any other schema.org type can be used too; it gets the generic properties, and any schema.org property can be mapped.",
            }, SchemaOrgPartSettingsDisplayDriver.EditorJsonOptions);
        }

        var configurator = arguments.Services.GetRequiredService<SchemaOrgConfigurator>();
        var description = await configurator.DescribeAsync(contentType, schemaType);

        return description is null
            ? $"There is no content type named '{contentType}'. Call this tool without a content type to list them."
            : JsonSerializer.Serialize(description, SchemaOrgPartSettingsDisplayDriver.EditorJsonOptions);
    }
}
