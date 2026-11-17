using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Approvals.Workflows;

/// <summary>
/// Starts when an approval request is withdrawn or cancelled; Note is the reason. The workflow's correlation id is the request id.
/// </summary>
public sealed class ApprovalCanceledEvent : ApprovalEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ApprovalCanceledEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ApprovalCanceledEvent(IStringLocalizer<ApprovalCanceledEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(ApprovalCanceledEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Approval Canceled"];
}
