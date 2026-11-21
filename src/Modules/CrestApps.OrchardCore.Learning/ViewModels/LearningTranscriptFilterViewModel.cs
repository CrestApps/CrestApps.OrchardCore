namespace CrestApps.OrchardCore.Learning.ViewModels;

/// <summary>
/// The learner filter of the Learner transcript report.
/// </summary>
public class LearningTranscriptFilterViewModel
{
    /// <summary>
    /// Gets or sets the user name of the learner, or <see langword="null"/> for everyone.
    /// </summary>
    public string Learner { get; set; }
}
