using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Checkout.Json;
using CrestApps.OrchardCore.Payments.Models;

namespace CrestApps.OrchardCore.Checkout;

/// <summary>
/// Identifies a distinct recurring billing interval (for example "every 1 month" or "every 3 weeks").
/// Line items that share a billing duration are billed together as one recurring obligation.
/// </summary>
[JsonConverter(typeof(BillingDurationKeyJsonConverter))]
public sealed class BillingDurationKey : IEquatable<BillingDurationKey>
{
    /// <summary>
    /// The number of <see cref="Type"/> units in one billing cycle.
    /// </summary>
    public int Duration { get; }

    /// <summary>
    /// The unit of time that <see cref="Duration"/> is expressed in.
    /// </summary>
    public DurationType Type { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="BillingDurationKey"/> class.
    /// </summary>
    /// <param name="type">The unit of time for the billing cycle.</param>
    /// <param name="duration">The number of time units in one billing cycle.</param>
    public BillingDurationKey(DurationType type, int duration)
    {
        Type = type;
        Duration = duration;
    }

    /// <inheritdoc/>
    public override bool Equals(object obj)
        => Equals(obj as BillingDurationKey);

    /// <inheritdoc/>
    public bool Equals(BillingDurationKey other)
        => other != null && Duration == other.Duration && Type == other.Type;

    /// <inheritdoc/>
    public override int GetHashCode()
        => HashCode.Combine(Duration, Type);
}
