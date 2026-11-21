using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;

namespace CrestApps.OrchardCore.Surveys.Core.Services;

/// <summary>
/// Tells response observers about grading and accommodation changes once the change is saved.
/// </summary>
public interface ISurveyScoringEventDispatcher
{
    /// <summary>
    /// Tells observers after the current scope's changes are saved.
    /// </summary>
    /// <param name="notify">What to tell each observer.</param>
    void Dispatch(Func<ISurveyResponseObserver, Task> notify);

    /// <summary>
    /// Tells observers that an accommodation was granted, after the change is saved.
    /// </summary>
    /// <param name="surveyOverride">The accommodation.</param>
    void OverrideGranted(SurveyOverride surveyOverride)
        => Dispatch(observer => observer.OverrideGrantedAsync(surveyOverride));
}
