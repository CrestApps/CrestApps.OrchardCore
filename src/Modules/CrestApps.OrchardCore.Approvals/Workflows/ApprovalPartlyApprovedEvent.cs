using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Approvals.Workflows;

/// <summary>
/// Starts when an approver approves and more approvals are needed in the same stage. The workflow's correlation id is the request id.
/// </summary>
public sealed class ApprovalPartlyApprovedEvent : ApprovalEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ApprovalPartlyApprovedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ApprovalPartlyApprovedEvent(IStringLocalizer<ApprovalPartlyApprovedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(ApprovalPartlyApprovedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Approval Partly Approved"];
}
