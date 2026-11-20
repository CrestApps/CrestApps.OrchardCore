using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Banking.Disbursements.Workflows;

/// <summary>
/// Starts when the bank's returns file is applied; each returned disbursement starts Disbursement Returned too.
/// </summary>
public sealed class DisbursementReturnsAppliedEvent : DisbursementEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DisbursementReturnsAppliedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DisbursementReturnsAppliedEvent(IStringLocalizer<DisbursementReturnsAppliedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(DisbursementReturnsAppliedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Disbursement Returns Applied"];
}
