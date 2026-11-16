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
                ("Kind", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The approver rule provider: Permission, Role, Users, Manager or Staff (with Staff on), or one a module adds.")),
                ("Value", Text("The permission's name, the role's name, or user or staff member ids separated by commas. Empty for Manager.")))
            .Required("Kind")
            .AdditionalProperties(false);

        var stage = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("Title", Text("What people see, such as Manager or Finance.")),
                ("Approvers", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(rule).Description("Who may decide the stage: a person any rule names may.")),
                ("RequiredApprovals", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Minimum(1).Maximum(10).Description("How many different approvers must approve the stage.")))
            .Required("Approvers")
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
                            ("Types", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.String)).Description("The approval types the policy serves, such as PayRun or Ticket; empty for every type.")),
                            ("Enabled", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the policy is chosen for requests that name none. On by default.")),
                            ("Order", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("Where the policy comes among those that match; lower first.")),
                            ("MinimumAmount", new JsonSchemaBuilder().Type(SchemaValueType.Number | SchemaValueType.Null).Description("The least amount a request must have for the policy to be chosen.")),
                            ("MaximumAmount", new JsonSchemaBuilder().Type(SchemaValueType.Number | SchemaValueType.Null).Description("The most amount a request may have for the policy to be chosen.")),
                            ("Currency", Text("The currency a request's amount must be in for the policy to be chosen.")),
                            ("AccessScope", Text("The access scope a request must have for the policy to be chosen, such as an employer's id.")),
                            ("Stages", new JsonSchemaBuilder().Type(SchemaValueType.Array).MaxItems(10).Items(stage).Description("The stages, decided one after the other. Omit them for one stage made of Approvers and RequiredApprovals.")),
                            ("Approvers", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(rule).Description("Who may decide a policy without Stages: a person any rule names may.")),
                            ("RequiredApprovals", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Minimum(1).Maximum(10).Description("How many different approvers must approve a policy without Stages.")),
                            ("AllowRequesterToApprove", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the person who asked may approve their own request. Off by default.")),
                            ("AllowSubjectToDecide", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether a person may decide a request about themselves. Off by default.")),
                            ("DueAfterHours", new JsonSchemaBuilder().Type(SchemaValueType.Integer | SchemaValueType.Null).Minimum(1).Description("Hours after which a request is due, for reminders.")))
                        .Required("Name")
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
