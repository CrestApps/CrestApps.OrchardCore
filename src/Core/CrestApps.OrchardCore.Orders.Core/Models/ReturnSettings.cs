namespace CrestApps.OrchardCore.Orders.Core.Models;

/// <summary>
/// Settings › Commerce › Returns.
/// </summary>
public sealed class ReturnSettings
{
    /// <summary>
    /// Gets or sets for how many days after an order is paid its buyer can ask to return items.
    /// </summary>
    public int WindowDays { get; set; } = 30;

    /// <summary>
    /// The reasons offered until the store sets its own.
    /// </summary>
    public static readonly IReadOnlyList<string> DefaultReasons = ["Doesn't fit", "Not as described", "Arrived damaged", "Changed my mind", "Other"];

    /// <summary>
    /// Gets or sets the reasons the store set. Empty until it sets them: settings are read into a new object, and a
    /// list that starts with the defaults would gain the saved reasons after them, listing every reason twice.
    /// </summary>
    public List<string> Reasons { get; set; } = [];

    /// <summary>
    /// Returns the reasons a buyer chooses from: the store's, once each, or the defaults.
    /// </summary>
    public IReadOnlyList<string> GetReasons()
        => Reasons is { Count: > 0 } ? [.. Reasons.Distinct(StringComparer.OrdinalIgnoreCase)] : DefaultReasons;

    /// <summary>
    /// Gets or sets whether a buyer can ask for something else instead of their money back.
    /// </summary>
    public bool AllowExchanges { get; set; } = true;

    /// <summary>
    /// Gets or sets what the buyer is told once the return is approved, such as where to send the items.
    /// </summary>
    public string Instructions { get; set; }
}
