using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Parts;

/// <summary>
/// Represents the audit trail part schema.
/// </summary>
public sealed class AuditTrailPartSchema : PartSchemaDefinitionBase
{
    public override string Name { get; } = "AuditTrailPart";

    protected override JsonSchemaBuilder BuildSettingsCore()
    {
        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("AuditTrailPartSettings", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("ShowCommentInput", new JsonSchemaBuilder()
                            .Type(SchemaValueType.Boolean)
                            .Default(true)
                            .Description("Show the comment input field.")))
                    .AdditionalProperties(false)))
            .AdditionalProperties(true);
    }

    protected override JsonSchemaBuilder BuildPartSchemaCore()
    {
        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("Comment", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                ("ShowComment", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)))
            .AdditionalProperties(true);
    }
}
