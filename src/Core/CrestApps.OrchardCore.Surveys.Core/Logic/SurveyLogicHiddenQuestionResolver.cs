using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Core.Logic;

/// <summary>
/// Tells the scoring engine which questions a response hid, with the survey logic engine, so hidden questions do not
/// count toward the most points.
/// </summary>
public sealed class SurveyLogicHiddenQuestionResolver : ISurveyHiddenQuestionResolver
{
    private readonly SurveyLogicEngine _logic;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyLogicHiddenQuestionResolver"/> class.
    /// </summary>
    /// <param name="logic">The logic engine.</param>
    public SurveyLogicHiddenQuestionResolver(SurveyLogicEngine logic)
    {
        _logic = logic;
    }

    /// <inheritdoc />
    public IReadOnlySet<string> GetHiddenQuestionIds(SurveyDefinition definition, SurveyResponse response)
    {
        if (definition is null || response is null)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        return _logic.Evaluate(definition, response).HiddenQuestionIds;
    }
}
