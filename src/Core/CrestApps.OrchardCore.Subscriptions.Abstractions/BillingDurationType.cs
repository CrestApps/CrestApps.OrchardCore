using System.Text.Json.Serialization;
using CrestApps.Support.Json;

namespace CrestApps.OrchardCore.Subscriptions;

[JsonConverter(typeof(BidirectionalJsonStringEnumConverterFactory))]
public enum BillingDurationType
{
    Year,
    Month,
    Week,
    Day,
}

public class BillingDurationKey : IEquatable<BillingDurationKey>
{
    public int Duration { get; set; }
    public BillingDurationType Type { get; set; }

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
