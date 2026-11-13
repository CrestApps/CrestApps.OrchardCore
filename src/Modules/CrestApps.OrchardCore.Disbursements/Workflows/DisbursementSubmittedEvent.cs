using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Disbursements.Workflows;

/// <summary>
/// Starts when a disbursement is sent: written to a bank file, paid by an issued check, or handed to a provider.
/// </summary>
public sealed class DisbursementSubmittedEvent : DisbursementEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DisbursementSubmittedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DisbursementSubmittedEvent(IStringLocalizer<DisbursementSubmittedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(DisbursementSubmittedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Disbursement Submitted"];
}
