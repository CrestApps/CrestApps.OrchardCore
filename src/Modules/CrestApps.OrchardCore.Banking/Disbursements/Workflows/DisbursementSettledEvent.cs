using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Banking.Disbursements.Workflows;

/// <summary>
/// Starts when a disbursement is paid: its batch settled, its check cleared, or it was paid outside the system.
/// </summary>
public sealed class DisbursementSettledEvent : DisbursementEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DisbursementSettledEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DisbursementSettledEvent(IStringLocalizer<DisbursementSettledEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(DisbursementSettledEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Disbursement Settled"];
}
