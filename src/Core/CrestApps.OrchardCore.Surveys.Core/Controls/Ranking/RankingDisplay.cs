namespace CrestApps.OrchardCore.Surveys.Core.Controls.Ranking;

/// <summary>
/// How a ranking question is answered.
/// </summary>
public enum RankingDisplay
{
    /// <summary>
    /// Items are dragged into order (with buttons to move them for keyboards and screen readers).
    /// </summary>
    DragList,

    /// <summary>
    /// A rank is picked for each item.
    /// </summary>
    Selects,
}
