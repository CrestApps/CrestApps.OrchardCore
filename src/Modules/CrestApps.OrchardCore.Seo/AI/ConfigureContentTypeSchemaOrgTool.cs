using System.Text.Json;
using CrestApps.OrchardCore.Seo.Drivers;
using CrestApps.OrchardCore.Seo.Models;
using CrestApps.OrchardCore.Seo.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Seo.AI;

/// <summary>
/// Lets an AI agent describe a content type's items with schema.org: it adds the Schema.org part, chooses the type and
/// maps the properties to the content type's fields and values, filling in the suggested mappings it was not given.
/// </summary>
public sealed class ConfigureContentTypeSchemaOrgTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "configureContentTypeSchemaOrg";

    /// <summary>
    /// Orchard Core's permission to edit content types, which changing a type's schema.org settings needs.
    /// </summary>
    internal static readonly Permission EditContentTypes = new("EditContentTypes", LocalizationSource.Create<ConfigureContentTypeSchemaOrgTool>("Edit content types."), isSecurityCritical: true);

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "contentType": {
          "type": "string",
          "description": "The technical name of the content type, such as BlogPost."
        },
        "schemaType": {
          "type": "string",
          "description": "The schema.org type of its items, such as BlogPosting, Event, Product or Recipe."
        },
        "properties": {
          "type": "array",
          "description": "The properties to map. Each replaces the mapping of the same name. Leave it out to keep the current mappings.",
          "items": {
            "type": "object",
            "properties": {
              "name": {
                "type": "string",
                "description": "The schema.org property, such as headline, or a dotted name for a nested one, such as offers.price."
              },
              "source": {
                "type": "string",
                "enum": [ "Content", "Fixed", "Editor", "Liquid" ],
                "description": "Content: a value of each item, given by a source key from getSchemaOrgOptions, such as Item:DisplayText or Field:BlogPost.Image. Fixed: the same value for every item. Editor: editors enter it on each item. Liquid: a Liquid template that can use ContentItem, Url and BaseUrl."
              },
              "value": {
                "type": "string",
                "description": "The source key, the fixed value or the Liquid template. Leave it out for Editor."
              }
            },
            "required": [ "name", "source" ]
          }
        },
        "replace": {
          "type": "boolean",
          "description": "true to replace all the current mappings with the given ones; false (the default) to merge them."
        },
        "addSuggested": {
          "type": "boolean",
          "description": "true (the default) to also map the required and recommended properties that are still unmapped, as the editor suggests."
        }
      },
      "required": [ "contentType", "schemaType" ]
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Configures the schema.org structured data (JSON-LD) a content type's pages publish: adds the Schema.org part to the content type if needed, sets its schema.org type, and maps its properties to the content type's fields and values. Unless told otherwise, it also maps the suggested required and recommended properties. Returns the saved settings and the required properties still missing. Call getSchemaOrgOptions first to see the content type's values.";

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

        var user = arguments.Services.GetRequiredService<IHttpContextAccessor>().HttpContext?.User;

        if (user is null || !await arguments.Services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(user, EditContentTypes))
        {
            return "You do not have permission to edit content types.";
        }

        var contentType = SchemaOrgToolArguments.GetString(arguments, "contentType");
        var schemaType = SchemaOrgToolArguments.GetString(arguments, "schemaType");

        if (contentType is null || schemaType is null)
        {
            return "Both contentType and schemaType are required.";
        }

        if (!SchemaOrgToolArguments.TryGetJson<List<SchemaOrgPropertyMapping>>(arguments, "properties", SchemaOrgPartSettingsDisplayDriver.EditorJsonOptions, out var properties))
        {
            return "The properties could not be read. Send an array of objects with name, source and value.";
        }

        var result = await arguments.Services.GetRequiredService<SchemaOrgConfigurator>().ConfigureAsync(
            contentType,
            schemaType,
            properties,
            SchemaOrgToolArguments.GetBoolean(arguments, "replace") ?? false,
            SchemaOrgToolArguments.GetBoolean(arguments, "addSuggested") ?? true);

        if (result.Errors.Count > 0)
        {
            return "Nothing was changed: " + string.Join(" ", result.Errors);
        }

        return JsonSerializer.Serialize(new
        {
            saved = true,
            contentType,
            partAdded = result.PartAdded,
            result.Settings.SchemaType,
            result.Settings.Properties,
            missingRequired = result.MissingRequired,
            note = result.MissingRequired.Count > 0
                ? "Search engines require the properties in missingRequired for rich results. Map them, or map them with the Editor source so editors fill them in."
                : null,
        }, SchemaOrgPartSettingsDisplayDriver.EditorJsonOptions);
    }
}
