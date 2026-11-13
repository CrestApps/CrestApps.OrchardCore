using CrestApps.OrchardCore.Recipes.Core;
using Json.Schema;

namespace CrestApps.OrchardCore.Disbursements.Recipes;

/// <summary>
/// Describes the "Disbursements" recipe step to CrestApps Recipes.
/// </summary>
internal sealed class DisbursementsRecipeStepSchema : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => DisbursementsConstants.RecipeStepName;

    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(DisbursementsConstants.RecipeStepName).Description("Recipe step discriminator. Must be 'Disbursements'.")),
                (DisbursementsRecipeStep.DisbursementsProperty, new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("itemId", Text("The disbursement's id.")),
                            ("idempotencyKey", Text("The key that identifies the disbursement; one already on the site is skipped.")),
                            ("payeeName", Text("The payee's name.")),
                            ("isBusinessPayee", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the payee is a business.")),
                            ("amount", new JsonSchemaBuilder().Type(SchemaValueType.Number).Description("The amount.")),
                            ("currency", Text("The three-letter currency code.")),
                            ("method", Text("How it was sent: Ach, Check, Manual or a provider's method.")),
                            ("provider", Text("The provider that sent it.")),
                            ("sourceBankAccountId", Text("The id of the business's bank account it left from.")),
                            ("destinationBankAccountId", Text("The id of the payee's bank account.")),
                            ("sourceKind", Text("The kind of record it paid.")),
                            ("sourceId", Text("The id of the record it paid.")),
                            ("accessScope", Text("The access scope within the source kind.")),
                            ("memo", Text("The memo.")),
                            ("status", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Pending", "Batched", "Submitted", "Settled", "Returned", "Failed", "Canceled", "Reversed").Description("The status.")),
                            ("effectiveDate", Text("The date the money arrived, yyyy-MM-dd.")),
                            ("traceNumber", Text("The trace number the bank knew it by.")),
                            ("createdUtc", Text("When it was recorded (UTC).")))
                        .Required("idempotencyKey", "method", "currency")
                        .AdditionalProperties(true))
                    .Description("The disbursements to add as history. They are never batched or changed. Account numbers are never part of them.")))
            .Required("name", DisbursementsRecipeStep.DisbursementsProperty)
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }

    private static JsonSchemaBuilder Text(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description(description);
}
