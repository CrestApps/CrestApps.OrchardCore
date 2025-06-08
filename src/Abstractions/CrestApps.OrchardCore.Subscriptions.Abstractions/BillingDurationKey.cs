using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Payments.Models;
using Json;

namespace CrestApps.OrchardCore.Subscriptions;

[JsonConverter(typeof(BillingDurationKeyJsonConverter))]

public class BillingDurationKey : IEquatable<BillingDurationKey>
{
    public int Duration { get; }

    public DurationType Type { get; }

    public BillingDurationKey(DurationType type, int duration)
    {
        Type = type;
        Duration = duration;
    }

    public override bool Equals(object obj)
    {
        return Equals(obj as BillingDurationKey);
    }

    public bool Equals(BillingDurationKey other)
    {
        return other != null &&
               Duration == other.Duration &&
               Type == other.Type;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Duration, Type);
    }
}
