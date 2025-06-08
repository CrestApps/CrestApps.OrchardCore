using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.Payments.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DurationType
{
    Year,
    Month,
    Week,
    Day,
}
