using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Banking.Disbursements.Workflows;

/// <summary>
/// Starts when a settled disbursement is taken back. Disbursement Failed starts too.
/// </summary>
public sealed class DisbursementReversedEvent : DisbursementEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DisbursementReversedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DisbursementReversedEvent(IStringLocalizer<DisbursementReversedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(DisbursementReversedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Disbursement Reversed"];
}
