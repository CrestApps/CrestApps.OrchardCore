using System.Text.Json.Nodes;
using Json.Schema;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Parts;

/// <summary>
/// Represents the bag part schema.
/// </summary>
public sealed class BagPartSchema : PartSchemaDefinitionBase, IContainedContentPartSchemaDefinition
{
    public override string Name { get; } = "BagPart";

    public string NestedItemsPropertyName => "ContentItems";

    protected override JsonSchemaBuilder BuildSettingsCore()
    {
        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("BagPartSettings", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("ContainedContentTypes", new JsonSchemaBuilder()
                            .Type(SchemaValueType.Array)
                            .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))),
                        ("ContainedStereotypes", new JsonSchemaBuilder()
                            .Type(SchemaValueType.Array)
                            .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))),
                        ("DisplayType", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                        ("CollapseContainedItems", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)))
                    .AdditionalProperties(false)),
                ("BagPartBlocksEditorSettings", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("AddButtonText", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                        ("ModalTitleText", new JsonSchemaBuilder().Type(SchemaValueType.String)))
                    .AdditionalProperties(false)
                )
            )
            .AdditionalProperties(true);
    }

    protected override JsonSchemaBuilder BuildPartSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .AdditionalProperties(true);

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<string>> GetContainedContentTypesAsync(
        ContentPartSchemaContext context,
        IReadOnlyList<ContentTypeDefinition> knownContentTypeDefinitions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(knownContentTypeDefinitions);

        var containedContentTypes = GetStringSettings(context.ContentTypePartDefinition.Settings, "BagPartSettings", "ContainedContentTypes");
        var containedStereotypes = GetStringSettings(context.ContentTypePartDefinition.Settings, "BagPartSettings", "ContainedStereotypes");
        var allowedContentTypes = new HashSet<string>(containedContentTypes, StringComparer.OrdinalIgnoreCase);

        if (containedStereotypes.Length > 0)
        {
            foreach (var knownDefinition in knownContentTypeDefinitions.Where(contentTypeDefinition =>
                containedStereotypes.Contains(GetContentTypeStereotype(contentTypeDefinition), StringComparer.OrdinalIgnoreCase)))
            {
                allowedContentTypes.Add(knownDefinition.Name);
            }
        }

        return ValueTask.FromResult<IReadOnlyList<string>>([.. allowedContentTypes.OrderBy(value => value, StringComparer.Ordinal)]);
    }

    private static string GetContentTypeStereotype(ContentTypeDefinition definition)
    {
        if (definition.Settings is null ||
            !definition.Settings.TryGetPropertyValue("ContentTypeSettings", out var settingsNode) ||
            settingsNode is not JsonObject settingsObject ||
            !settingsObject.TryGetPropertyValue("Stereotype", out var stereotypeNode))
        {
            return null;
        }

        return stereotypeNode?.GetValue<string>();
    }
}
