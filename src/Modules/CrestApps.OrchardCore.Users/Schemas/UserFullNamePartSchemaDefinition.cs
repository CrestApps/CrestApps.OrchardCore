using CrestApps.OrchardCore.Recipes.Core.Schemas.Parts;
using CrestApps.OrchardCore.Users.Core.Models;
using Json.Schema;

namespace CrestApps.OrchardCore.Users.Schemas;

/// <summary>
/// Provides recipe schema support for the <see cref="UserFullNamePart"/> payload.
/// </summary>
public sealed class UserFullNamePartSchemaDefinition : PartSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name => nameof(UserFullNamePart);

    protected override JsonSchemaBuilder BuildSettingsCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .AdditionalProperties(true);

    protected override JsonSchemaBuilder BuildPartSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("DisplayName", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Description("The display name shown for the user.")),
                ("FirstName", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Description("The user's first name.")),
                ("LastName", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Description("The user's last name.")),
                ("MiddleName", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Description("The user's middle name.")))
            .AdditionalProperties(true);
}
