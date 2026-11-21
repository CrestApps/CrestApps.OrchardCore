using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Tests.Surveys.TestSupport;

/// <summary>
/// Hides the questions a test names.
/// </summary>
internal sealed class ScoringHiddenResolver : ISurveyHiddenQuestionResolver
{
    public HashSet<string> Hidden { get; } = new(StringComparer.Ordinal);

    public IReadOnlySet<string> GetHiddenQuestionIds(SurveyDefinition definition, SurveyResponse response)
        => Hidden;
}
