namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// How the live dashboard names an agent's own phone call where it names a Contact Center interaction everywhere else:
/// <c>phone:{monitored user}:{call}</c>. The monitored user is part of it because an extension call is one call with an
/// agent at each end, and a supervisor listens to one of them.
/// </summary>
public static class PhoneCallKey
{
    /// <summary>
    /// The prefix every phone call key starts with. No interaction identifier does.
    /// </summary>
    public const string Prefix = "phone:";

    /// <summary>
    /// Names an agent's phone call.
    /// </summary>
    /// <param name="userId">The agent being monitored.</param>
    /// <param name="callId">The call.</param>
    /// <returns>The key.</returns>
    public static string Create(string userId, string callId)
        => $"{Prefix}{userId}:{callId}";

    /// <summary>
    /// Whether a dashboard identifier names a phone call rather than an interaction.
    /// </summary>
    /// <param name="value">The identifier.</param>
    /// <returns><see langword="true"/> for a phone call key.</returns>
    public static bool IsPhoneCall(string value)
        => !string.IsNullOrEmpty(value) && value.StartsWith(Prefix, StringComparison.Ordinal);

    /// <summary>
    /// Reads a phone call key. The call identifier may itself contain colons; the user identifier never does.
    /// </summary>
    /// <param name="value">The key.</param>
    /// <param name="userId">The agent being monitored.</param>
    /// <param name="callId">The call.</param>
    /// <returns><see langword="true"/> when the key names both.</returns>
    public static bool TryParse(string value, out string userId, out string callId)
    {
        userId = null;
        callId = null;

        if (!IsPhoneCall(value))
        {
            return false;
        }

        var rest = value[Prefix.Length..];
        var separator = rest.IndexOf(':', StringComparison.Ordinal);

        if (separator <= 0 || separator == rest.Length - 1)
        {
            return false;
        }

        userId = rest[..separator];
        callId = rest[(separator + 1)..];

        return true;
    }
}
