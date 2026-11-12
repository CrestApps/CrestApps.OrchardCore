using System.Globalization;
using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.Users;

/// <summary>
/// Records that a user proved they control their email address by a round trip: they followed a confirmation link, or
/// signed in with a code emailed to them. <c>EmailConfirmed</c> is not proof on its own, because a site that doesn't ask
/// users to confirm their email marks it confirmed at registration. Anything that hands a user what belongs to an email
/// address (guest orders, a guest's customer record) requires this marker.
/// </summary>
public static class UserEmailOwnership
{
    /// <summary>
    /// The key of the marker in the user's properties.
    /// </summary>
    public const string PropertyName = "EmailOwnershipVerifiedUtc";

    /// <summary>
    /// Whether the user proved they control their email address.
    /// </summary>
    /// <param name="properties">The user's properties.</param>
    public static bool IsVerified(JsonObject properties)
        => properties?[PropertyName] is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrEmpty(text);

    /// <summary>
    /// Records that the user proved they control their email address.
    /// </summary>
    /// <param name="properties">The user's properties.</param>
    /// <param name="verifiedUtc">When they proved it.</param>
    public static void MarkVerified(JsonObject properties, DateTime verifiedUtc)
    {
        ArgumentNullException.ThrowIfNull(properties);

        properties[PropertyName] = verifiedUtc.ToString("O", CultureInfo.InvariantCulture);
    }
}
