using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Payments.Models;
using Json;

namespace CrestApps.OrchardCore.Subscriptions;

/// <summary>
/// Represents a billing interval key made from a duration unit and a duration count.
/// </summary>
[JsonConverter(typeof(BillingDurationKeyJsonConverter))]
public class BillingDurationKey : IEquatable<BillingDurationKey>
{
    /// <summary>
    /// Gets the number of duration units in the billing interval.
    /// </summary>
    public int Duration { get; }

    /// <summary>
    /// Gets the unit used by the billing interval.
    /// </summary>
    public DurationType Type { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="BillingDurationKey"/> class.
    /// </summary>
    /// <param name="type">The unit used by the billing interval.</param>
    /// <param name="duration">The number of duration units in the billing interval.</param>
    public BillingDurationKey(DurationType type, int duration)
    {
        Type = type;
        Duration = duration;
    }

    /// <summary>
    /// Determines whether the specified object represents the same billing interval.
    /// </summary>
    /// <param name="obj">The object to compare with the current billing interval key.</param>
    /// <returns><see langword="true"/> when the object has the same duration and type; otherwise, <see langword="false"/>.</returns>
    public override bool Equals(object obj)
    {
        return Equals(obj as BillingDurationKey);
    }

    /// <summary>
    /// Determines whether the specified billing interval key has the same duration and type.
    /// </summary>
    /// <param name="other">The billing interval key to compare with the current instance.</param>
    /// <returns><see langword="true"/> when both keys use the same duration and type; otherwise, <see langword="false"/>.</returns>
    public bool Equals(BillingDurationKey other)
    {
        return other != null &&
               Duration == other.Duration &&
               Type == other.Type;
    }

    /// <summary>
    /// Returns a hash code based on the billing interval duration and type.
    /// </summary>
    /// <returns>A hash code for the current billing interval key.</returns>
    public override int GetHashCode()
    {
        return HashCode.Combine(Duration, Type);
    }
}
