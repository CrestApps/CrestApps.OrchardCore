using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Approvals.Workflows;

/// <summary>
/// Starts when someone asks for an approval. The workflow's correlation id is the request id.
/// </summary>
public sealed class ApprovalRequestedEvent : ApprovalEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ApprovalRequestedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ApprovalRequestedEvent(IStringLocalizer<ApprovalRequestedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(ApprovalRequestedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Approval Requested"];
}
