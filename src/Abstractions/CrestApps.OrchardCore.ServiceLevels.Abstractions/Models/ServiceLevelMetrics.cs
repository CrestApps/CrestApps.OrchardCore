namespace CrestApps.OrchardCore.ServiceLevels.Models;

/// <summary>
/// The well-known metrics a service level policy sets targets for. The metric is an open string, so a module can add
/// its own.
/// </summary>
public static class ServiceLevelMetrics
{
    /// <summary>
    /// The time until the first response to a new request.
    /// </summary>
    public const string FirstResponse = "FirstResponse";

    /// <summary>
    /// The time until the next response, each time the requester writes again.
    /// </summary>
    public const string NextResponse = "NextResponse";

    /// <summary>
    /// The time until the request is resolved.
    /// </summary>
    public const string Resolution = "Resolution";
}
