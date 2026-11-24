using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Approvals.Core.Services;
using CrestApps.OrchardCore.Approvals.Models;
using CrestApps.OrchardCore.Approvals.Services;
using CrestApps.OrchardCore.SourceAccess;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Approvals.AI;

/// <summary>
/// Gets one approval request as its page (Approvals › Requests › the request) shows it, with the page's rule for who may
/// open it (<see cref="ApprovalRequestAccess.CanSeeAsync"/> at the View level): a request the user may not open is
/// answered as one that does not exist.
/// </summary>
internal sealed class GetApprovalRequestTool : ApprovalsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "getApprovalRequest";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "requestId": {
          "type": "string",
          "description": "The approval request's id, from listApprovalRequests."
        }
      },
      "required": ["requestId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Gets one approval request by id, as its page shows it: what is asked, the policy and its stages with the approvals each has, every decision with its note, the status, and whether the signed-in user may decide it now (and why not) or withdraw it. Use it before deciding.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <summary>
    /// The answer for a request that does not exist or that the user may not open.
    /// </summary>
    /// <param name="requestId">The id that was asked for.</param>
    /// <returns>The sentence.</returns>
    public static string NotFound(string requestId)
        => $"No approval request has the id '{requestId}' that you can see. Use listApprovalRequests to find it.";

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        if (ReadString(arguments, "requestId") is not { } requestId)
        {
            return "Nothing was read: give the request's id. Use listApprovalRequests to find it.";
        }

        var access = services.GetRequiredService<ApprovalRequestAccess>();

        if (await services.GetRequiredService<IApprovalRequestManager>().FindByIdAsync(requestId, cancellationToken) is not { } request
            || !await access.CanSeeAsync(request, caller, SourceAccessLevel.View, cancellationToken))
        {
            return NotFound(requestId);
        }

        var eligibility = await services.GetRequiredService<IApprovalService>().GetEligibilityAsync(request, caller, cancellationToken);
        var detail = await ApprovalRequestResults.DescribeAsync<ApprovalRequestDetail>(services, request, cancellationToken);
        var stages = request.GetStages();

        detail.WithdrawReason = request.Status == ApprovalStatus.Canceled ? request.CancelReason : null;
        detail.ClosedAt = await ApprovalRequestResults.LocalTimeAsync(services, request.ClosedUtc);
        detail.CanDecide = eligibility.CanDecide;
        detail.WhyNot = eligibility.CanDecide ? null : eligibility.Reason;
        detail.CanWithdraw = await access.CanWithdrawAsync(request, caller, cancellationToken);
        detail.Note = ApprovalRequestResults.UntrustedNote;

        // Each stage's state, as the request's page writes it.
        for (var index = 0; index < stages.Count; index++)
        {
            var stageIndex = index;

            detail.Stages.Add(new ApprovalStageDetail
            {
                Number = index + 1,
                Title = stages[index].Title,
                Required = Math.Max(1, stages[index].RequiredApprovals),
                Approvals = request.Decisions.Count(decision => decision.Stage == stageIndex && decision.Kind == ApprovalDecisionKind.Approve),
                State = request.Status == ApprovalStatus.Approved || index < request.CurrentStage
                    ? "approved"
                    : index == request.CurrentStage
                        ? request.Status == ApprovalStatus.Pending ? "waiting" : "closed"
                        : "notStarted",
            });
        }

        foreach (var decision in request.Decisions)
        {
            var stage = stages[Math.Clamp(decision.Stage, 0, stages.Count - 1)];

            detail.Decisions.Add(new ApprovalDecisionDetail
            {
                By = decision.UserName,
                Decision = decision.Kind == ApprovalDecisionKind.Approve ? "approved" : "rejected",
                Stage = decision.Stage + 1,
                StageTitle = stage.Title,
                At = await ApprovalRequestResults.LocalTimeAsync(services, decision.DecidedUtc),
                Note = decision.Note,
            });
        }

        return Json(detail);
    }
}

/// <summary>
/// An approval request with its stages, decisions and what the user may do with it.
/// </summary>
internal sealed class ApprovalRequestDetail : ApprovalRequestItem
{
    /// <summary>Gets or sets why it was withdrawn, written by the person who withdrew it.</summary>
    public string WithdrawReason { get; set; }

    /// <summary>Gets or sets when it was decided or withdrawn, in local time.</summary>
    public string ClosedAt { get; set; }

    /// <summary>Gets the stages.</summary>
    public List<ApprovalStageDetail> Stages { get; } = [];

    /// <summary>Gets the decisions made.</summary>
    public List<ApprovalDecisionDetail> Decisions { get; } = [];

    /// <summary>Gets or sets whether the user may decide it now.</summary>
    public bool CanDecide { get; set; }

    /// <summary>Gets or sets why the user may not decide it.</summary>
    public string WhyNot { get; set; }

    /// <summary>Gets or sets whether the user may withdraw it on its page.</summary>
    public bool CanWithdraw { get; set; }

    /// <summary>Gets or sets how to read the text people wrote.</summary>
    public string Note { get; set; }
}

/// <summary>
/// One stage of a request.
/// </summary>
internal sealed class ApprovalStageDetail
{
    /// <summary>Gets or sets the stage's number, from 1.</summary>
    public int Number { get; set; }

    /// <summary>Gets or sets the stage's title.</summary>
    public string Title { get; set; }

    /// <summary>Gets or sets how many approvals it needs.</summary>
    public int Required { get; set; }

    /// <summary>Gets or sets how many approvals it has.</summary>
    public int Approvals { get; set; }

    /// <summary>Gets or sets its state: approved, waiting, closed or notStarted.</summary>
    public string State { get; set; }
}

/// <summary>
/// One decision on a request.
/// </summary>
internal sealed class ApprovalDecisionDetail
{
    /// <summary>Gets or sets the approver's name.</summary>
    public string By { get; set; }

    /// <summary>Gets or sets the decision: approved or rejected.</summary>
    public string Decision { get; set; }

    /// <summary>Gets or sets the number of the stage it was made in, from 1.</summary>
    public int Stage { get; set; }

    /// <summary>Gets or sets the title of that stage.</summary>
    public string StageTitle { get; set; }

    /// <summary>Gets or sets when it was made, in local time.</summary>
    public string At { get; set; }

    /// <summary>Gets or sets the approver's note.</summary>
    public string Note { get; set; }
}
