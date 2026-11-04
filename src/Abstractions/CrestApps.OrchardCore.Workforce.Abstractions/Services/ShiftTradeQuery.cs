using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// Which shift trades to list.
/// </summary>
public sealed class ShiftTradeQuery
{
    /// <summary>
    /// Gets or sets the teams; <see langword="null"/> for every team.
    /// </summary>
    public IReadOnlyCollection<string> TeamIds { get; set; }

    /// <summary>
    /// Gets or sets the status; <see langword="null"/> for any.
    /// </summary>
    public ShiftTradeStatus? Status { get; set; }

    /// <summary>
    /// Gets or sets a worker who offered it or to whom it is offered; <see langword="null"/> for anyone.
    /// </summary>
    public string WorkerId { get; set; }
}
