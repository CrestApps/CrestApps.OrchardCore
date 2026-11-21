namespace CrestApps.OrchardCore.Surveys;

/// <summary>
/// The built-in kinds of survey. A kind is an open string: modules may use their own.
/// </summary>
public static class SurveyKinds
{
    /// <summary>
    /// A survey or form.
    /// </summary>
    public const string Survey = "Survey";

    /// <summary>
    /// A scored quiz or exam.
    /// </summary>
    public const string Quiz = "Quiz";

    /// <summary>
    /// A one-question poll whose totals respondents may see.
    /// </summary>
    public const string Poll = "Poll";
}
