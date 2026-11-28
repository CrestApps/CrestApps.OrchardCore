using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// One row of the versions list.
/// </summary>
public class SurveyVersionRowViewModel
{
    /// <summary>
    /// Gets or sets the version.
    /// </summary>
    public SurveyVersion Version { get; set; }

    /// <summary>
    /// Gets or sets how many responses are pinned to it.
    /// </summary>
    public int Responses { get; set; }

    /// <summary>
    /// Gets or sets the name of who published it.
    /// </summary>
    public string PublishedBy { get; set; }

    /// <summary>
    /// Gets or sets whether it is the published version.
    /// </summary>
    public bool IsCurrent { get; set; }

    /// <summary>
    /// Gets or sets the stamp (ticks) of the survey's draft when the list was shown, so a restore refuses a draft changed
    /// since.
    /// </summary>
    public long? DraftStamp { get; set; }
}
