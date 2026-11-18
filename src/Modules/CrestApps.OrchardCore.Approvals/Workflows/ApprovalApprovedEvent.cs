using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Approvals.Workflows;

/// <summary>
/// Starts when a request has the approvals it needs.
/// </summary>
public sealed class ApprovalApprovedEvent : ApprovalEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ApprovalApprovedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ApprovalApprovedEvent(IStringLocalizer<ApprovalApprovedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(ApprovalApprovedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Approval Approved"];
}
