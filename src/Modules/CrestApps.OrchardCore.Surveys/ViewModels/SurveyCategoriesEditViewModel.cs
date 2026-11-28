using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// The score categories and bands of a survey definition.
/// </summary>
public class SurveyCategoriesEditViewModel
{
    /// <summary>
    /// Gets or sets the score categories.
    /// </summary>
    public List<ScoreCategory> Categories { get; set; } = [];

    /// <summary>
    /// Gets or sets the score bands.
    /// </summary>
    public List<ScoreBand> Bands { get; set; } = [];
}
