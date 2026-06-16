using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Represents the audit trail part schema.
/// </summary>
public sealed class AuditTrailPartSchema : PartSettingsSchemaBase
{
    public override string Name { get; } = "AuditTrailPart";

    protected override JsonSchemaBuilder BuildSettingsCore()
    {
        return Envelope("AuditTrailPartSettings",
            Obj(
                Prop("ShowCommentInput", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Boolean)
                    .Default(true)
                    .Description("Show the comment input field.")))
            );
    }
}
