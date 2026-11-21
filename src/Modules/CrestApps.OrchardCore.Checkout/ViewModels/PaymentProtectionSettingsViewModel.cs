using CrestApps.OrchardCore.Checkout.Models;

namespace CrestApps.OrchardCore.Checkout.ViewModels;

/// <summary>
/// The editor of <see cref="PaymentProtectionSettings"/>.
/// </summary>
public class PaymentProtectionSettingsViewModel
{
    /// <summary>
    /// Gets or sets what happens during a store-wide run of declined card payments.
    /// </summary>
    public DeclineSpikeAction OnDeclineSpike { get; set; }
}
