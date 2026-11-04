using CrestApps.OrchardCore.Accounts.Deployments;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Json.Schema;

namespace CrestApps.OrchardCore.Accounts.Recipes;

/// <summary>
/// Describes the Accounts recipe step to CrestApps Recipes.
/// </summary>
internal sealed class AccountsRecipeStepSchema : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => AccountsStep.Name;

    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(AccountsStep.Name).Description("Recipe step discriminator. Must be 'Accounts'.")),
                ("Accounts", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(ContentItem("An account: a content item with AccountPart, and the parts other modules add to it, such as AccountMembersPart, AccountLocationsPart and B2BAccountPart."))
                    .Description("The accounts to create or update. An account with an existing ContentItemId is updated.")),
                ("Records", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(ContentItem("A record an account holds, such as a contact or an opportunity. Its ContainedPart names the account."))
                    .Description("The records the accounts hold, imported after the accounts.")))
            .Required("name", "Accounts")
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }

    private static JsonSchemaBuilder ContentItem(string description)
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("ContentItemId", new JsonSchemaBuilder().Type(SchemaValueType.String).MinLength(1).Description("The content item id, which stays the same across sites.")),
                ("ContentItemVersionId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The version id.")),
                ("ContentType", new JsonSchemaBuilder().Type(SchemaValueType.String).MinLength(1).Description("The content type.")),
                ("DisplayText", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The item's title.")),
                ("Latest", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)),
                ("Published", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)))
            .Required("ContentItemId", "ContentType")
            .AdditionalProperties(true)
            .Description(description);
}

/// <summary>
/// Describes the Accounts deployment step to CrestApps Recipes.
/// </summary>
internal sealed class AccountsDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(AccountsDeploymentStep);

    protected override string DisplayText => "Accounts";

    protected override string Description => "Exports every account with its members, locations and other parts, and optionally the records the accounts hold.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
        =>
        [
            (nameof(AccountsDeploymentStep.IncludeRecords), new JsonSchemaBuilder()
                .Type(SchemaValueType.Boolean)
                .Default(true)
                .Description("Whether the records the accounts hold, such as their contacts and opportunities, are exported too.")),
        ];
}
