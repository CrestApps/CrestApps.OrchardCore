namespace CrestApps.OrchardCore.Workforce.ViewModels;

/// <summary>
/// A shift trade with who can accept it.
/// </summary>
public class ShiftTradeDetailsViewModel
{
    /// <summary>
    /// Gets or sets the trade and the names in it.
    /// </summary>
    public ShiftTradeRowViewModel Row { get; set; }

    /// <summary>
    /// Gets or sets the colleagues who could accept it, with why each can't.
    /// </summary>
    public IList<ShiftTradeCandidateViewModel> Candidates { get; set; } = [];
}

/// <summary>
/// A colleague who could accept a trade.
/// </summary>
public class ShiftTradeCandidateViewModel
{
    /// <summary>
    /// Gets or sets the worker id.
    /// </summary>
    public string WorkerId { get; set; }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets why they can't accept; empty when they can.
    /// </summary>
    public IList<string> Problems { get; set; } = [];
}
