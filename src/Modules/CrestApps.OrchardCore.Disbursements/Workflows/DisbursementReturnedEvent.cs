using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Disbursements.Workflows;

/// <summary>
/// Starts when the bank returns a disbursement, such as for a closed account. FailureCode holds the return code.
/// </summary>
public sealed class DisbursementReturnedEvent : DisbursementEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DisbursementReturnedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DisbursementReturnedEvent(IStringLocalizer<DisbursementReturnedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(DisbursementReturnedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Disbursement Returned"];
}
