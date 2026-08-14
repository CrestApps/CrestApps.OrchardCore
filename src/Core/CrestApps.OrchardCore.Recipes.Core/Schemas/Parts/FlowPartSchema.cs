using Json.Schema;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Parts;

/// <summary>
/// Represents the flow part schema.
/// </summary>
public sealed class FlowPartSchema : PartSchemaDefinitionBase, IContainedContentPartSchemaDefinition
{
    public override string Name { get; } = "FlowPart";

    public string NestedItemsPropertyName => "Widgets";

    protected override JsonSchemaBuilder BuildSettingsCore()
    {
        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("FlowPartSettings", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("ContainedContentTypes", new JsonSchemaBuilder()
                            .Type(SchemaValueType.Array)
                            .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))),
                        ("CollapseContainedItems", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)),
                        ("DefaultAlignment", new JsonSchemaBuilder()
                            .Type(SchemaValueType.String)
                            .Enum("Left", "Center", "Right", "Justify", "Inherit")))
                    .AdditionalProperties(false)))
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

        return ValueTask.FromResult<IReadOnlyList<string>>(GetStringSettings(
            context.ContentTypePartDefinition.Settings,
            "FlowPartSettings",
            "ContainedContentTypes"));
    }
}
