using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Core.Services;

/// <summary>
/// Hides no question.
/// </summary>
public sealed class NullSurveyHiddenQuestionResolver : ISurveyHiddenQuestionResolver
{
    private static readonly IReadOnlySet<string> _none = new HashSet<string>(StringComparer.Ordinal);

    /// <inheritdoc />
    public IReadOnlySet<string> GetHiddenQuestionIds(SurveyDefinition definition, SurveyResponse response)
        => _none;
}
