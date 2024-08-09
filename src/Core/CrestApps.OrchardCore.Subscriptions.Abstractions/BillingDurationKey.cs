using System.Text.Json.Serialization;
using Json;

namespace CrestApps.OrchardCore.Subscriptions;

[JsonConverter(typeof(BillingDurationKeyJsonConverter))]

public class BillingDurationKey : IEquatable<BillingDurationKey>
{
    public int Duration { get; }

    public BillingDurationType Type { get; }

    public BillingDurationKey(BillingDurationType type, int duration)
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
