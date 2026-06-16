using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Fields;

/// <summary>
/// Represents the taxonomy field schema.
/// </summary>
public sealed class TaxonomyFieldSchema : FieldSchemaDefinitionBase
{
    public override string Name { get; } = "TaxonomyField";

    protected override JsonSchemaBuilder BuildSettingsCore()
    {
        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("TaxonomyFieldSettings", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("Hint", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                        ("Required", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)),
                        ("TaxonomyContentItemId", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                        ("Unique", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)),
                        ("LeavesOnly", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)),
                        ("Open", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)),
                        ("Placeholder", new JsonSchemaBuilder().Type(SchemaValueType.String)))
                    .AdditionalProperties(false)),
                ("TaxonomyFieldTagsEditorSettings", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(("Open", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)))
                    .AdditionalProperties(false)))
            .AdditionalProperties(true);
    }

    protected override JsonSchemaBuilder BuildFieldSchemaCore()
    {
        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("TaxonomyContentItemId", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                ("TermContentItemIds", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))),
                ("TagNames", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))))
            .AdditionalProperties(true);
    }
}
