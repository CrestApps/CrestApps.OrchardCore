namespace CrestApps.OrchardCore.Surveys.Core.Runtime;

/// <summary>
/// The kinds of <see cref="SurveyCounter"/>.
/// </summary>
public static class SurveyCounterKinds
{
    /// <summary>
    /// How many times each question of an evenly presented pool was presented, by question id.
    /// </summary>
    public const string Pools = "Pools";

    /// <summary>
    /// How many completed responses each quota holds, by quota id.
    /// </summary>
    public const string Quotas = "Quotas";
}
