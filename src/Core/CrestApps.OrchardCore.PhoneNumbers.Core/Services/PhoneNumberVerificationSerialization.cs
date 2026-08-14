using System.Text.Json;

namespace CrestApps.OrchardCore.PhoneNumbers.Core.Services;

/// <summary>
/// Provides shared JSON serialization settings for persisting verification results.
/// </summary>
public static class PhoneNumberVerificationSerialization
{
    /// <summary>
    /// Gets the serializer options used to persist and read verification results.
    /// </summary>
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
    };
}
