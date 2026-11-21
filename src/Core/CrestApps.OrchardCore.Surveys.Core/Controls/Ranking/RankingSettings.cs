namespace CrestApps.OrchardCore.Surveys.Core.Controls.Ranking;

/// <summary>
/// The settings of a ranking question. Its items are the question's choices.
/// </summary>
public sealed class RankingSettings
{
    /// <summary>
    /// Gets or sets how many items the respondent ranks, or <see langword="null"/> for all.
    /// </summary>
    public int? MaxRanked { get; set; }

    /// <summary>
    /// Gets or sets how the question is answered.
    /// </summary>
    public RankingDisplay Display { get; set; }
}
