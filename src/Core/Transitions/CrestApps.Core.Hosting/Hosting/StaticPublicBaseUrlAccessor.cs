namespace CrestApps.Core.Hosting;

/// <summary>
/// The default <see cref="IPublicBaseUrlAccessor"/>, reporting an address fixed at startup.
/// </summary>
/// <remarks>
/// For a host whose public address comes from its own configuration rather than from a per-tenant
/// store. A trailing slash is trimmed so callers can append a path without producing a double slash.
/// </remarks>
public sealed class StaticPublicBaseUrlAccessor : IPublicBaseUrlAccessor
{
    private readonly string _baseUrl;

    /// <summary>
    /// Initializes a new instance of the <see cref="StaticPublicBaseUrlAccessor"/> class.
    /// </summary>
    /// <param name="baseUrl">The public base URL, or <see langword="null"/> when there is none.</param>
    public StaticPublicBaseUrlAccessor(string baseUrl)
    {
        _baseUrl = string.IsNullOrWhiteSpace(baseUrl) ? null : baseUrl.TrimEnd('/');
    }

    /// <inheritdoc/>
    public ValueTask<string> GetBaseUrlAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromResult(_baseUrl);
}
