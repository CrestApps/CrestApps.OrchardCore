using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Banking.Disbursements.Workflows;

/// <summary>
/// Starts when a disbursement is recorded.
/// </summary>
public sealed class DisbursementCreatedEvent : DisbursementEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DisbursementCreatedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DisbursementCreatedEvent(IStringLocalizer<DisbursementCreatedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(DisbursementCreatedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Disbursement Created"];
}
