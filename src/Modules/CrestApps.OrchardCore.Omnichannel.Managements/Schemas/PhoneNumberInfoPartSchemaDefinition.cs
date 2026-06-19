using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Parts;
using Json.Schema;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Schemas;

/// <summary>
/// Provides recipe schema support for the <c>PhoneNumberInfoPart</c> payload.
/// </summary>
public sealed class PhoneNumberInfoPartSchemaDefinition : PartSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name => OmnichannelConstants.ContentParts.PhoneNumberInfo;

    protected override JsonSchemaBuilder BuildSettingsCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .AdditionalProperties(true);

    protected override JsonSchemaBuilder BuildPartSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("Number", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("PhoneNumber", new JsonSchemaBuilder()
                            .Type(SchemaValueType.String)
                            .Description("The phone number in E.164 format (e.g., \"+14155552671\").")),
                        ("CountryCode", new JsonSchemaBuilder()
                            .Type(SchemaValueType.String)
                            .Description("The ISO 3166-1 alpha-2 country code (e.g., \"US\", \"CA\").")),
                        ("NationalNumber", new JsonSchemaBuilder()
                            .Type(SchemaValueType.String)
                            .Description("The national phone number without the country calling code.")))
                    .AdditionalProperties(true)),
                ("Extension", CreateTextFieldSchema("The phone extension text.")),
                ("Type", CreateTextFieldSchema("The phone number type text, such as Mobile or Home.")))
            .AdditionalProperties(true);

    private static JsonSchemaBuilder CreateTextFieldSchema(string description)
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("Text", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Description(description)))
            .AdditionalProperties(true);
}
