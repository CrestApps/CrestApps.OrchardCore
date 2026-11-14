using System.Globalization;
using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.Users;

/// <summary>
/// Records that a user proved they control their email address by a round trip: they followed a confirmation link, or
/// signed in with a code emailed to them. <c>EmailConfirmed</c> is not proof on its own, because a site that doesn't ask
/// users to confirm their email marks it confirmed at registration. Anything that hands a user what belongs to an email
/// address (guest orders, a guest's customer record) requires this marker. The marker names the address that was
/// proved, so it stops counting when the user's email is changed without a new round trip, as Orchard Core's own profile
/// editor and an administrator's edit both allow.
/// </summary>
public static class UserEmailOwnership
{
    /// <summary>
    /// The key of the marker in the user's properties: when the address was proved.
    /// </summary>
    public const string PropertyName = "EmailOwnershipVerifiedUtc";

    /// <summary>
    /// The key of the address that was proved, normalized, in the user's properties.
    /// </summary>
    public const string EmailPropertyName = "EmailOwnershipVerifiedEmail";

    /// <summary>
    /// Whether the user proved they control <paramref name="email"/>, their current address.
    /// </summary>
    /// <param name="properties">The user's properties.</param>
    /// <param name="email">The user's email address now.</param>
    public static bool IsVerified(JsonObject properties, string email)
        => properties?[PropertyName] is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrEmpty(text) &&
            properties[EmailPropertyName] is JsonValue proved && proved.TryGetValue<string>(out var provedEmail) &&
            Normalize(email) is { } current && string.Equals(provedEmail, current, StringComparison.Ordinal);

    /// <summary>
    /// Records that the user proved they control <paramref name="email"/>.
    /// </summary>
    /// <param name="properties">The user's properties.</param>
    /// <param name="email">The address they proved.</param>
    /// <param name="verifiedUtc">When they proved it.</param>
    public static void MarkVerified(JsonObject properties, string email, DateTime verifiedUtc)
    {
        ArgumentNullException.ThrowIfNull(properties);

        if (Normalize(email) is not { } normalized)
        {
            return;
        }

        properties[PropertyName] = verifiedUtc.ToString("O", CultureInfo.InvariantCulture);
        properties[EmailPropertyName] = normalized;
    }

    private static string Normalize(string email)
        => string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToUpperInvariant();
}