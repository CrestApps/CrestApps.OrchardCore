using CrestApps.OrchardCore.Recipes.Core.Schemas.Parts;
using CrestApps.OrchardCore.Roles.Core.Models;
using Json.Schema;

namespace CrestApps.OrchardCore.Roles.Schemas;

/// <summary>
/// Provides recipe schema support for the <see cref="RolePickerPart"/> payload.
/// </summary>
public sealed class RolePickerPartSchemaDefinition : PartSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name => nameof(RolePickerPart);

    protected override JsonSchemaBuilder BuildSettingsCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("RolePickerPartSettings", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("Required", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether at least one role must be picked.")),
                        ("AllowSelectMultiple", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether more than one role may be picked.")),
                        ("ExcludedRoles", new JsonSchemaBuilder()
                            .Type(SchemaValueType.Array, SchemaValueType.Null)
                            .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))
                            .Description("The role names that are not offered.")),
                        ("Hint", new JsonSchemaBuilder().Type(SchemaValueType.String, SchemaValueType.Null).Description("The hint shown under the picker.")))
                    .AdditionalProperties(false)))
            .AdditionalProperties(true);

    protected override JsonSchemaBuilder BuildPartSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("RoleNames", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array, SchemaValueType.Null)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.String))
                    .Description("The selected role names.")))
            .AdditionalProperties(true);
}
