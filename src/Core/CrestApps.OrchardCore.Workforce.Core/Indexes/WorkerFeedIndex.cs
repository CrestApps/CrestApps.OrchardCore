using YesSql.Indexes;

namespace CrestApps.OrchardCore.Workforce.Core.Indexes;

/// <summary>
/// The SHA-256 of a worker's calendar feed secret, to find the worker from the feed's address.
/// </summary>
public sealed class WorkerFeedIndex : MapIndex
{
    /// <summary>
    /// Gets or sets the worker.
    /// </summary>
    public string WorkerId { get; set; }

    /// <summary>
    /// Gets or sets the SHA-256 of the secret.
    /// </summary>
    public string TokenHash { get; set; }
}
