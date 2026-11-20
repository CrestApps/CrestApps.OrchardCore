using CrestApps.OrchardCore.Recipes.Core;
using Json.Schema;

namespace CrestApps.OrchardCore.Banking.Checks.Recipes;

/// <summary>
/// Describes the "CheckRegister" recipe step to CrestApps Recipes.
/// </summary>
internal sealed class CheckRegisterRecipeStepSchema : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => ChecksConstants.RegisterRecipeStepName;

    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(ChecksConstants.RegisterRecipeStepName).Description("Recipe step discriminator. Must be 'CheckRegister'.")),
                (CheckRegisterRecipeStep.ChecksProperty, new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("itemId", Text("The check's id.")),
                            ("bankAccountId", Text("The id of the bank account the check is drawn on.")),
                            ("bankAccountName", Text("The bank account's name.")),
                            ("number", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Minimum(1).Description("The check number.")),
                            ("payeeName", Text("The payee's name.")),
                            ("amount", new JsonSchemaBuilder().Type(SchemaValueType.Number).Description("The amount.")),
                            ("currency", Text("The three-letter currency code.")),
                            ("checkDate", Text("The check date, yyyy-MM-dd.")),
                            ("memo", Text("The memo.")),
                            ("sourceKind", Text("The kind of record the check paid.")),
                            ("sourceId", Text("The id of the record the check paid.")),
                            ("status", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Issued", "Printed", "Voided", "StopPayment", "Cleared", "Reissued", "Stale").Description("The status.")),
                            ("issuedUtc", Text("When the check was issued (UTC).")),
                            ("clearedOn", Text("The date the bank paid it, yyyy-MM-dd.")))
                        .Required("bankAccountId", "number")
                        .AdditionalProperties(true))
                    .Description("The checks to add to the register as history. They are never printed or changed.")))
            .Required("name", CheckRegisterRecipeStep.ChecksProperty)
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }

    private static JsonSchemaBuilder Text(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description(description);
}
