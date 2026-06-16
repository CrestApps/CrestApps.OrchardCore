using CrestApps.OrchardCore.Recipes.Core.Schemas.Fields;
using Json.Schema;

namespace CrestApps.OrchardCore.ContentFields.Schemas;

internal sealed class PhoneFieldSchemaDefinition : FieldSchemaDefinitionBase
{
    public override string Name => "PhoneField";

    protected override JsonSchemaBuilder BuildSettingsCore()
    {
        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("PhoneFieldSettings", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("Hint", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                        ("Required", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)),
                        ("InitialCountryMode", new JsonSchemaBuilder()
                            .Type(SchemaValueType.String)
                            .Enum("Globe", "CurrentCulture", "Specific")),
                        ("SpecificCountryCode", new JsonSchemaBuilder().Type(SchemaValueType.String)))
                    .AdditionalProperties(false)))
            .AdditionalProperties(true);
    }

    protected override JsonSchemaBuilder BuildFieldSchemaCore()
    {
        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("PhoneNumber", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                ("CountryCode", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                ("NationalNumber", new JsonSchemaBuilder().Type(SchemaValueType.String)))
            .AdditionalProperties(true);
    }
}
