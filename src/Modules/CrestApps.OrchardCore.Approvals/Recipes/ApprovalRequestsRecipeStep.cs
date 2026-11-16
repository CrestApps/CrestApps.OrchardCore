using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Approvals.Models;
using CrestApps.OrchardCore.Approvals.Services;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Approvals.Recipes;

/// <summary>
/// Imports approval requests as history, such as the decisions another system kept before the move. Imported
/// requests are never decided again, send no notice and start no workflow. One whose id already exists is skipped.
/// </summary>
internal sealed class ApprovalRequestsRecipeStep : NamedRecipeStepHandler
{
    /// <summary>
    /// The property that holds the requests.
    /// </summary>
    public const string RequestsProperty = "Requests";

    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly IApprovalRequestManager _manager;

    public ApprovalRequestsRecipeStep(IApprovalRequestManager manager)
        : base(ApprovalsConstants.RequestsRecipeStepName)
    {
        _manager = manager;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        foreach (var node in (context.Step[RequestsProperty] as JsonArray ?? []).OfType<JsonObject>())
        {
            var values = node.Deserialize<ApprovalRequest>(JsonOptions);

            if (values is null || string.IsNullOrEmpty(values.SubjectKind) || string.IsNullOrEmpty(values.SubjectId))
            {
                context.Errors.Add($"{ApprovalsConstants.RequestsRecipeStepName}: a request needs a SubjectKind and a SubjectId.");

                continue;
            }

            if (!string.IsNullOrEmpty(values.ItemId) && await _manager.FindByIdAsync(values.ItemId) is not null)
            {
                continue;
            }

            var request = await _manager.NewAsync();

            if (!string.IsNullOrEmpty(values.ItemId))
            {
                request.ItemId = values.ItemId;
            }

            request.SubjectKind = values.SubjectKind;
            request.SubjectId = values.SubjectId;
            request.AccessScope = values.AccessScope;
            request.SubjectUrl = values.SubjectUrl;
            request.SubjectUserIds = values.SubjectUserIds ?? [];
            request.SubjectStaffMemberId = values.SubjectStaffMemberId;
            request.Title = values.Title;
            request.Amount = values.Amount;
            request.Currency = values.Currency;
            request.PolicyId = values.PolicyId;
            request.PolicyTitle = values.PolicyTitle;
            request.RequiredApprovals = Math.Max(1, values.RequiredApprovals);
            request.Stages = values.Stages ?? [];
            request.CurrentStage = Math.Max(0, values.CurrentStage);
            request.RequesterUserId = values.RequesterUserId;
            request.RequesterName = values.RequesterName;

            // History is decided: a pending request from elsewhere has no approver here to finish it.
            request.Status = values.Status == ApprovalStatus.Pending ? ApprovalStatus.Canceled : values.Status;
            request.CancelReason = values.Status == ApprovalStatus.Pending ? "Imported while pending" : values.CancelReason;
            request.Decisions = values.Decisions ?? [];
            request.CreatedUtc = values.CreatedUtc;
            request.DueUtc = values.DueUtc;
            request.ClosedUtc = values.ClosedUtc ?? values.CreatedUtc;
            request.IsHistorical = true;

            await _manager.CreateAsync(request);
        }
    }
}
