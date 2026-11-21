using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Services;

namespace CrestApps.OrchardCore.Tests.Surveys.TestSupport;

/// <summary>
/// A control registry for scoring tests that knows only the fake scorer.
/// </summary>
internal sealed class ScoringFakeRegistry : ISurveyControlRegistry
{
    private readonly ScoringFakeScorer _scorer = new();

    public IReadOnlyList<SurveyControlDescriptor> GetControls() => [];

    public IReadOnlyList<SurveyControlPresetDescriptor> GetPresets() => [];

    public SurveyControlDescriptor GetControl(string controlType) => null;

    public SurveyControlPresetDescriptor GetPreset(string name) => null;

    public ISurveyControlHandler GetHandler(string controlType) => null;

    public ISurveyControlScorer GetScorer(string controlType)
        => controlType == ScoringFakeScorer.Type ? _scorer : null;

    public ISurveyControlAnalyzer GetAnalyzer(string controlType) => null;
}
