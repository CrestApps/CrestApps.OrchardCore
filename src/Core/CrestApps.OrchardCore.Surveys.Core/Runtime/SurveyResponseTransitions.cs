using CrestApps.OrchardCore.StateMachines;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Core.Runtime;

/// <summary>
/// The moves a response's status may make.
/// </summary>
public static class SurveyResponseTransitions
{
    private static readonly TransitionTable<SurveyResponseStatus> _table = new(new Dictionary<SurveyResponseStatus, SurveyResponseStatus[]>
    {
        [SurveyResponseStatus.InProgress] = [SurveyResponseStatus.Completed, SurveyResponseStatus.ScreenedOut, SurveyResponseStatus.Abandoned, SurveyResponseStatus.Voided],
        [SurveyResponseStatus.Completed] = [SurveyResponseStatus.Voided, SurveyResponseStatus.InProgress],
        [SurveyResponseStatus.ScreenedOut] = [SurveyResponseStatus.Voided],
        [SurveyResponseStatus.Abandoned] = [SurveyResponseStatus.Voided],
        [SurveyResponseStatus.Voided] = [],
    });

    /// <summary>
    /// Gets the table of moves: in progress to completed, screened out, abandoned or voided; completed to voided or
    /// back in progress (reopened); screened out and abandoned to voided.
    /// </summary>
    public static TransitionTable<SurveyResponseStatus> Table
        => _table;
}
