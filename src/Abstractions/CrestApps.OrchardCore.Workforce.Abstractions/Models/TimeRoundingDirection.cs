namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// Which way punch times round.
/// </summary>
public enum TimeRoundingDirection
{
    /// <summary>
    /// To the nearest step, both ways.
    /// </summary>
    Nearest,

    /// <summary>
    /// Starts round down and ends round up, so the worker never loses time.
    /// </summary>
    FavorWorker,
}
