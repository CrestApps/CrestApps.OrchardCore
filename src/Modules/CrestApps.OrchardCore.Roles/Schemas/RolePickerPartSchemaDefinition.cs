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
                        ("Required", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)),
                        ("AllowSelectMultiple", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)),
                        ("ExcludedRoles", new JsonSchemaBuilder()
                            .Type(SchemaValueType.Array)
                            .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))),
                        ("Hint", new JsonSchemaBuilder().Type(SchemaValueType.String)))
                    .AdditionalProperties(false)))
            .AdditionalProperties(true);

    protected override JsonSchemaBuilder BuildPartSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("RoleNames", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.String))
                    .Description("The selected role names.")))
            .AdditionalProperties(true);
}
