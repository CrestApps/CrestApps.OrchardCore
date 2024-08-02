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
