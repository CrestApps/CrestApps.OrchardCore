using CrestApps.OrchardCore.StateMachines;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Core.Services;

/// <summary>
/// The moves a survey's status may make. Draft becomes Open only by publishing.
/// </summary>
public static class SurveyStatusTransitions
{
    /// <summary>
    /// The moves staff may make by hand.
    /// </summary>
    public static readonly TransitionTable<SurveyStatus> Manual = new(new Dictionary<SurveyStatus, SurveyStatus[]>
    {
        [SurveyStatus.Draft] = [],
        [SurveyStatus.Open] = [SurveyStatus.Closed],
        [SurveyStatus.Closed] = [SurveyStatus.Open, SurveyStatus.Archived],
        [SurveyStatus.Archived] = [SurveyStatus.Closed],
    });
}
