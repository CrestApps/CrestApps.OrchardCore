using CrestApps.OrchardCore.Recipes.Core.Schemas.Parts;
using CrestApps.OrchardCore.Roles.Core.Models;
using Json.Schema;

namespace CrestApps.OrchardCore.ContentAccessControl.Schemas;

/// <summary>
/// Adds recipe schema support for content-access-control settings contributed to <see cref="RolePickerPart"/>.
/// </summary>
public sealed class RolePickerPartContentAccessControlSchemaDefinition : PartSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name => nameof(RolePickerPart);

    protected override JsonSchemaBuilder BuildSettingsCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("RolePickerPartContentAccessControlSettings", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("IsContentRestricted", new JsonSchemaBuilder()
                            .Type(SchemaValueType.Boolean)
                            .Description("Whether content that uses this part is restricted to the selected roles.")))
                    .AdditionalProperties(false)))
            .AdditionalProperties(true);
}
