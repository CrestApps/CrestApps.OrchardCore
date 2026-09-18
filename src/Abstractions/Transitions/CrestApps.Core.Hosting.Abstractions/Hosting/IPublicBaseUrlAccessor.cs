namespace CrestApps.Core.Hosting;

/// <summary>
/// Reports the address the outside world reaches this tenant at.
/// </summary>
/// <remarks>
/// Needed wherever the suite has to hand a third party a URL to call back on - a media stream, a
/// webhook, a recording callback. That work often runs with no request in flight, so it cannot be
/// derived from the current request's host, and it must be operator-controlled rather than taken
/// from a header a caller can set.
/// </remarks>
public interface IPublicBaseUrlAccessor
{
    /// <summary>
    /// Gets the tenant's canonical public base URL.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// The base URL, or <see langword="null"/> when the host has none configured. A caller that
    /// cannot proceed without one is expected to say so rather than guess.
    /// </returns>
    ValueTask<string> GetBaseUrlAsync(CancellationToken cancellationToken = default);
}
