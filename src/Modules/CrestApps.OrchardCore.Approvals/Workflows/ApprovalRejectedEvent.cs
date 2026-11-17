using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Approvals.Workflows;

/// <summary>
/// Starts when an approver rejects a request.
/// </summary>
public sealed class ApprovalRejectedEvent : ApprovalEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ApprovalRejectedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ApprovalRejectedEvent(IStringLocalizer<ApprovalRejectedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(ApprovalRejectedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Approval Rejected"];
}
