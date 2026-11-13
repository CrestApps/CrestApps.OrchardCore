using CrestApps.OrchardCore.Numbering;
using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using Json.Schema;

namespace CrestApps.OrchardCore.BusinessDocuments.Recipes;

/// <summary>
/// Describes the <c>NumberSeriesSettings</c> member of Orchard Core's <c>Settings</c> recipe step to CrestApps Recipes.
/// </summary>
internal sealed class NumberSeriesSettingsSchema : SiteSettingsSchemaBase
{
    public override string Name => nameof(NumberSeriesSettings);

    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("The formats the site chose for its number series. A series without an entry uses the format it was registered with.")
            .Properties(
                (nameof(NumberSeriesSettings.Formats), new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Description("The chosen formats, by series name, such as Financial.Invoice.")
                    .AdditionalProperties(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            (nameof(NumberSeriesFormat.Pattern), new JsonSchemaBuilder()
                                .Type(SchemaValueType.String)
                                .MaxLength(NumberSeriesFormat.MaximumPatternLength)
                                .Description("The pattern the numbers are written with: text and the tokens {seq}, {seq:0000}, {yyyy}, {yy} and {MM}, with exactly one {seq} token.")),
                            (nameof(NumberSeriesFormat.FirstNumber), new JsonSchemaBuilder()
                                .Type(SchemaValueType.Integer)
                                .Minimum(1)
                                .Description("The first number of the series, and of each new year or month when the numbers start again.")),
                            (nameof(NumberSeriesFormat.Reset), new JsonSchemaBuilder()
                                .Type(SchemaValueType.String | SchemaValueType.Integer)
                                .Description("When the numbers start again: Never (0), Yearly (1) or Monthly (2). A yearly series needs {yyyy} or {yy}, and a monthly one {MM} too.")))
                        .AdditionalProperties(true))))
            .AdditionalProperties(true);
}
