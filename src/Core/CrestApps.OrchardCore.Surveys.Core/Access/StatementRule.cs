using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;

namespace CrestApps.OrchardCore.Surveys.Core.Access;

/// <summary>
/// Asks the respondent to accept the survey's statement (<see cref="SurveyAccessSettings.Statement"/>), such as an honor
/// statement, before a response starts. A check that only asks whether a start could be made
/// (<see cref="SurveyAccessContext.CheckOnly"/>) doesn't ask for it: the respondent accepts it on the start form.
/// </summary>
public sealed class StatementRule : ISurveyAccessRule
{
    /// <inheritdoc />
    public string Name => "Statement";

    /// <inheritdoc />
    public int Order => SurveyAccessRuleOrder.Statement;

    /// <inheritdoc />
    public Task<SurveyAccessDecision> CheckStartAsync(SurveyAccessContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.IsTest
            || context.CheckOnly
            || context.StatementAccepted
            || string.IsNullOrWhiteSpace(context.Survey?.Access?.Statement))
        {
            return Task.FromResult(SurveyAccessDecision.Allow);
        }

        return Task.FromResult(SurveyAccessDecision.Deny(SurveyErrorCode.StatementNotAccepted));
    }
}
