using CrestApps.OrchardCore.Recipes.Core;
using Json.Schema;

namespace CrestApps.OrchardCore.Checks.Recipes;

/// <summary>
/// Describes the "CheckLayouts" recipe step to CrestApps Recipes.
/// </summary>
internal sealed class CheckLayoutsRecipeStepSchema : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => ChecksConstants.LayoutsRecipeStepName;

    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        var position = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("X", Number("Distance from the left edge of the check, in points.")),
                ("Y", Number("Distance from the top edge of the check, in points.")),
                ("Width", Number("Width the field may take, in points; 0 for no limit.")),
                ("FontSize", Number("Text size, in points.")),
                ("Hidden", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the field is left off.")));

        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(ChecksConstants.LayoutsRecipeStepName).Description("Recipe step discriminator. Must be 'CheckLayouts'.")),
                (CheckLayoutsRecipeStep.EntriesName, new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("ItemId", Text("The layout's 26-character id; omit it to match by name or create one.")),
                            ("Name", Text("The layout's name.")),
                            ("Description", Text("Notes about the layout.")),
                            ("Stock", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Blank", "PrePrinted").Description("The paper.")),
                            ("Form", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("CheckOnTop", "CheckInMiddle", "CheckOnBottom", "ThreePerPage").Description("Where the check sits on the sheet.")),
                            ("Fields", new JsonSchemaBuilder().Type(SchemaValueType.Object).AdditionalProperties(position).Description("The place of each field by name: Date, Payee, PayeeAddress, Amount, AmountInWords, Memo, Number, Payer, Bank, Signature, SecondSignature.")),
                            ("OffsetX", Number("Calibration: how far everything moves right, in points.")),
                            ("OffsetY", Number("Calibration: how far everything moves down, in points.")),
                            ("PrintMicr", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the check line prints; it needs a check line font uploaded on the site.")),
                            ("MicrCountry", Text("The country whose check line format is used.")),
                            ("UseAuxiliaryOnUs", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the check number prints left of the routing number (business checks).")),
                            ("MicrFontSize", Number("The check line font size, in points.")),
                            ("MicrSymbolCharacters", Text("The four characters the font draws the transit, amount, on-us and dash symbols on.")),
                            ("PayerName", Text("The payer's name printed on blank stock.")),
                            ("PayerAddress", new JsonSchemaBuilder().Type(SchemaValueType.Object | SchemaValueType.Null).Description("The payer's address printed on blank stock.")),
                            ("SecondSignatureAbove", new JsonSchemaBuilder().Type(SchemaValueType.Number | SchemaValueType.Null).Description("The amount above which a second signature line prints.")))
                        .Required("Name")
                        .AdditionalProperties(true))
                    .Description("The check layouts to create, or update by id or name. Signatures and fonts are uploaded on each site.")))
            .Required("name", CheckLayoutsRecipeStep.EntriesName)
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }

    private static JsonSchemaBuilder Text(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description(description);

    private static JsonSchemaBuilder Number(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.Number).Description(description);
}
