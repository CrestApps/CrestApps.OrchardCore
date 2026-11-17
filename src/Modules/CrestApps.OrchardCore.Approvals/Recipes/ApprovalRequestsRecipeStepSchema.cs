using CrestApps.OrchardCore.Recipes.Core;
using Json.Schema;

namespace CrestApps.OrchardCore.Approvals.Recipes;

/// <summary>
/// Describes the "ApprovalRequests" recipe step to CrestApps Recipes.
/// </summary>
internal sealed class ApprovalRequestsRecipeStepSchema : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => ApprovalsConstants.RequestsRecipeStepName;

    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        var decision = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("userId", Text("The approver's user id.")),
                ("userName", Text("The approver's user name.")),
                ("kind", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Approve", "Reject").Description("The decision.")),
                ("note", Text("The approver's note.")),
                ("stage", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Minimum(0).Description("The index of the stage it was made in, from 0.")),
                ("decidedUtc", Text("When it was decided (UTC).")))
            .AdditionalProperties(true);

        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(ApprovalsConstants.RequestsRecipeStepName).Description("Recipe step discriminator. Must be 'ApprovalRequests'.")),
                (ApprovalRequestsRecipeStep.RequestsProperty, new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("itemId", Text("The request's id; one already on the site is skipped.")),
                            ("subjectKind", Text("The kind of record the request is about, such as PayRun.")),
                            ("subjectId", Text("The id of that record.")),
                            ("accessScope", Text("The access scope within the subject kind.")),
                            ("title", Text("What was asked.")),
                            ("policyId", Text("The policy's id.")),
                            ("policyTitle", Text("The policy's name when it was asked.")),
                            ("requiredApprovals", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("How many approvals it needed, for a request without stages.")),
                            ("stages", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder()
                                .Type(SchemaValueType.Object)
                                .Properties(
                                    ("title", Text("What people saw, such as Manager.")),
                                    ("approvers", new JsonSchemaBuilder().Type(SchemaValueType.Array).Description("Who could decide the stage: kind and value.")),
                                    ("requiredApprovals", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Minimum(1).Description("How many approvals the stage needed.")))
                                .AdditionalProperties(true)).Description("The stages the request went through, copied from its policy.")),
                            ("currentStage", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Minimum(0).Description("The index of the last stage decided, from 0.")),
                            ("requesterName", Text("Who asked.")),
                            ("status", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Pending", "Approved", "Rejected", "Canceled").Description("Where it ended. A pending request is imported as canceled.")),
                            ("decisions", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(decision).Description("The decisions, in order.")),
                            ("createdUtc", Text("When it was asked (UTC).")),
                            ("closedUtc", Text("When it was decided (UTC).")))
                        .Required("subjectKind", "subjectId")
                        .AdditionalProperties(true))
                    .Description("Approval requests to add as history: they are never decided again and send no notice.")))
            .Required("name", ApprovalRequestsRecipeStep.RequestsProperty)
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }

    private static JsonSchemaBuilder Text(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description(description);
}
