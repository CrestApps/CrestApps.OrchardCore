namespace CrestApps.OrchardCore.Checkout.Core.Services;

/// <summary>
/// Options that govern the <see cref="PaymentSessionCache"/> lifetime and the set of purposes cleared
/// when an entire session's signals are removed.
/// </summary>
public sealed class PaymentSessionCacheOptions
{
    /// <summary>
    /// The maximum lifetime of a cached checkout signal.
    /// </summary>
    public TimeSpan MaxLiveSession { get; set; } = TimeSpan.FromHours(2);

    /// <summary>
    /// The registered purposes cleared when a whole session is removed.
    /// </summary>
    public List<string> Purposes { get; } = [];
}
