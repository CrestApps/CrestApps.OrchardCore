using CrestApps.OrchardCore.Recipes.Core;
using Json.Schema;

namespace CrestApps.OrchardCore.Approvals.Recipes;

/// <summary>
/// Describes the "ApprovalPolicies" recipe step to CrestApps Recipes.
/// </summary>
internal sealed class ApprovalPoliciesRecipeStepSchema : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => ApprovalsConstants.PoliciesRecipeStepName;

    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        var rule = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("Kind", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Permission", "Role", "Manager", "Staff").Description("Who the rule names: anyone with a permission, anyone in a role, the subject's manager (and the managers above), or named staff members.")),
                ("Value", Text("The permission's name, the role's name, or the staff member ids separated by commas. Empty for Manager.")))
            .Required("Kind")
            .AdditionalProperties(false);

        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(ApprovalsConstants.PoliciesRecipeStepName).Description("Recipe step discriminator. Must be 'ApprovalPolicies'.")),
                (ApprovalPoliciesRecipeStep.EntriesName, new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("ItemId", Text("The policy's 26-character id; omit it to match by name or create one.")),
                            ("Name", Text("The technical name modules and workflows ask for, such as PayRunApproval.")),
                            ("Title", Text("The name people see.")),
                            ("Description", Text("What the policy is for.")),
                            ("Approvers", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(rule).Description("Who may decide: a person any rule names may.")),
                            ("RequiredApprovals", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Minimum(1).Maximum(10).Description("How many different approvers must approve.")),
                            ("AllowRequesterToApprove", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the person who asked may approve their own request. Off by default.")),
                            ("AllowSubjectToDecide", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether a person may decide a request about themselves. Off by default.")),
                            ("DueAfterHours", new JsonSchemaBuilder().Type(SchemaValueType.Integer | SchemaValueType.Null).Minimum(1).Description("Hours after which a request is due, for reminders.")))
                        .Required("Name", "Approvers")
                        .AdditionalProperties(true))
                    .Description("The approval policies to create, or update by id or name.")))
            .Required("name", ApprovalPoliciesRecipeStep.EntriesName)
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }

    private static JsonSchemaBuilder Text(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description(description);
}
