namespace CrestApps.OrchardCore.DataConnectors;

/// <summary>
/// The limits of reads and refreshes, bound from the <c>CrestApps:DataConnectors</c> configuration section.
/// </summary>
public sealed class DataConnectorsOptions
{
    /// <summary>
    /// Gets or sets the longest a refresh may run before it is canceled and fails.
    /// </summary>
    public TimeSpan RefreshTimeout { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Gets or sets the longest a live read may run before it is canceled.
    /// </summary>
    public TimeSpan LiveReadTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the number of rows a refresh reads and writes at a time.
    /// </summary>
    public int ImportBatchSize { get; set; } = 1_000;

    /// <summary>
    /// Gets or sets the number of the newest refreshes kept in each data set's history.
    /// </summary>
    public int RefreshRunsKept { get; set; } = 25;

    /// <summary>
    /// Gets or sets the most refreshes one run of the background task does; the rest wait for its next run.
    /// </summary>
    public int MaxRefreshesPerRun { get; set; } = 10;

    /// <summary>
    /// Gets or sets the number of rows the preview of a data set shows.
    /// </summary>
    public int PreviewRows { get; set; } = 20;
}

/// <summary>
/// Which hosts HTTP-based connectors may call, bound from the <c>CrestApps:DataConnectors:Network</c> configuration
/// section.
/// </summary>
/// <remarks>
/// By default a connector can't reach the site's own network (loopback, private, link-local and unspecified
/// addresses), so a connection can't be used to probe internal services. Allow the internal hosts a tenant needs by
/// name, or allow private networks for a site that runs inside one.
/// </remarks>
public sealed class DataConnectorNetworkOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether connectors may call hosts on private and loopback networks.
    /// </summary>
    public bool AllowPrivateNetworks { get; set; }

    /// <summary>
    /// Gets or sets host names (or IP addresses) connectors may call even when they resolve to a private network.
    /// </summary>
    public string[] AllowedHosts { get; set; } = [];

    /// <summary>
    /// Gets or sets the largest response, in bytes, a connector reads from one HTTP call.
    /// </summary>
    public long MaxResponseBytes { get; set; } = 100L * 1024 * 1024;
}
