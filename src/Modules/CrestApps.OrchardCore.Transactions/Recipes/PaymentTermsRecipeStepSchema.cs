using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Transactions.Deployments;
using Json.Schema;

namespace CrestApps.OrchardCore.Transactions.Recipes;

/// <summary>
/// Describes the PaymentTerms recipe step to CrestApps Recipes.
/// </summary>
internal sealed class PaymentTermsRecipeStepSchema : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => PaymentTermsDeploymentStep.StepName;

    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(PaymentTermsDeploymentStep.StepName).Description("Recipe step discriminator. Must be 'PaymentTerms'.")),
                ("Terms", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("ItemId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The term's 26-character id. When it matches a stored term, that term is updated; otherwise a new one is made under this id, so documents that name it keep working.")),
                            ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The name printed on documents, such as Net 30.")),
                            ("Description", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("A note about when to use the term.")),
                            ("Kind", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Integer).Description("How the due date follows from the issue date. One of: DueOnReceipt, NetDays, EndOfMonth, DayOfNextMonth (or its number 0 to 3).")),
                            ("Days", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Minimum(0).Maximum(3650).Description("Days after the issue date (NetDays) or after the end of the issue month (EndOfMonth).")),
                            ("DayOfMonth", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Minimum(0).Maximum(31).Description("The day of the next month the debt is due on (DayOfNextMonth), 1 to 31.")),
                            ("DiscountPercent", new JsonSchemaBuilder().Type(SchemaValueType.Number | SchemaValueType.Null).Minimum(0).Maximum(99.99m).Description("The percent taken off for paying early, such as 2; null for none.")),
                            ("DiscountDays", new JsonSchemaBuilder().Type(SchemaValueType.Integer | SchemaValueType.Null).Minimum(0).Description("The number of days after the issue date the discount is offered for, the last day included.")),
                            ("Enabled", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the term can be chosen for new documents.")))
                        .Required("Name")
                        .AdditionalProperties(true))
                    .Description("The payment terms to create or update.")))
            .Required("name", "Terms")
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }
}
