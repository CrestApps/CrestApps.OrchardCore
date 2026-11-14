namespace CrestApps.OrchardCore.Telnyx;

/// <summary>
/// The Telnyx REST API base address an administrator may set: an https address on <c>telnyx.com</c> or one of its
/// subdomains. Every Telnyx client sends the account's API key as a bearer token to this address, so any other host
/// (the cloud metadata service, an internal service) is refused when the setting is saved and ignored when it is read.
/// </summary>
public static class TelnyxApiAddress
{
    /// <summary>
    /// Gets whether an API base address may be saved. An empty one is allowed, and means the default.
    /// </summary>
    /// <param name="url">The address.</param>
    public static bool IsAllowed(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return true;
        }

        return Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) &&
            uri.Scheme == Uri.UriSchemeHttps &&
            string.IsNullOrEmpty(uri.UserInfo) &&
            (string.Equals(uri.IdnHost, "telnyx.com", StringComparison.OrdinalIgnoreCase) ||
             uri.IdnHost.EndsWith(".telnyx.com", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Returns the API base address to call, ending with <c>/</c>: the configured one when it is allowed, otherwise
    /// <see cref="TelnyxConstants.DefaultApiBaseUrl"/>.
    /// </summary>
    /// <param name="configured">The configured address, if any.</param>
    public static string Resolve(string configured)
    {
        if (string.IsNullOrWhiteSpace(configured) || !IsAllowed(configured))
        {
            return TelnyxConstants.DefaultApiBaseUrl;
        }

        var trimmed = configured.Trim();

        return trimmed.EndsWith('/') ? trimmed : trimmed + '/';
    }
}
