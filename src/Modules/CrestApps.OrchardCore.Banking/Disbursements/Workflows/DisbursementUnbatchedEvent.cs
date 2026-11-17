using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Banking.Disbursements.Workflows;

/// <summary>
/// Starts when a disbursement goes back to pending because its open batch was cancelled or reopened.
/// </summary>
public sealed class DisbursementUnbatchedEvent : DisbursementEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DisbursementUnbatchedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DisbursementUnbatchedEvent(IStringLocalizer<DisbursementUnbatchedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(DisbursementUnbatchedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Disbursement Removed from Batch"];
}
