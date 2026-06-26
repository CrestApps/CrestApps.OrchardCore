using System.Text.Json;

namespace CrestApps.OrchardCore.PhoneNumbers.Verifications.Services;

/// <summary>
/// Provides JSON serialization options for phone-number verification provider payloads.
/// </summary>
internal static class PhoneNumberVerificationProviderJsonSerializerOptions
{
    /// <summary>
    /// Gets the serializer options used for provider HTTP request and response payloads.
    /// </summary>
    public static JsonSerializerOptions Default { get; } = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };
}
