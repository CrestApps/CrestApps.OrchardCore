using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Services;
using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.Tests.Surveys.TestSupport;

/// <summary>
/// A question type registry with the types the bank tests use: choice types, a matching type with rows, a number, a
/// yes/no type and a text type, plus a preset.
/// </summary>
internal sealed class BanksControlRegistry : ISurveyControlRegistry
{
    private readonly Dictionary<string, (SurveyControlDescriptor Descriptor, ISurveyControlHandler Handler)> _controls = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SurveyControlPresetDescriptor> _presets = new(StringComparer.OrdinalIgnoreCase);

    public BanksControlRegistry()
    {
        Add(SurveyControlNames.SingleChoice, SurveyControlCapabilities.Answerable | SurveyControlCapabilities.UsesChoices | SurveyControlCapabilities.Scorable);
        Add(SurveyControlNames.MultipleChoice, SurveyControlCapabilities.Answerable | SurveyControlCapabilities.UsesChoices | SurveyControlCapabilities.Scorable);
        Add(SurveyControlNames.Ranking, SurveyControlCapabilities.Answerable | SurveyControlCapabilities.UsesChoices | SurveyControlCapabilities.Scorable);
        Add(SurveyControlNames.Matching, SurveyControlCapabilities.Answerable | SurveyControlCapabilities.UsesChoices | SurveyControlCapabilities.UsesRows | SurveyControlCapabilities.Scorable);
        Add(SurveyControlNames.Number, SurveyControlCapabilities.Answerable | SurveyControlCapabilities.Scorable, DataFieldType.Decimal);
        Add(SurveyControlNames.Boolean, SurveyControlCapabilities.Answerable | SurveyControlCapabilities.Scorable, DataFieldType.Boolean);
        Add(SurveyControlNames.ShortText, SurveyControlCapabilities.Answerable | SurveyControlCapabilities.Scorable);
        Add(SurveyControlNames.LongText, SurveyControlCapabilities.Answerable | SurveyControlCapabilities.ManuallyGraded);
        Add(SurveyControlNames.Rating, SurveyControlCapabilities.Answerable, DataFieldType.Integer);
        Add(SurveyControlNames.TextBlock, SurveyControlCapabilities.None);

        _presets[SurveyControlNames.Nps] = new SurveyControlPresetDescriptor
        {
            Name = SurveyControlNames.Nps,
            BaseControl = SurveyControlNames.Rating,
            Initialize = question => question.Settings["Max"] = 10,
        };
    }

    public IReadOnlyList<SurveyControlDescriptor> GetControls()
        => _controls.Values.Select(item => item.Descriptor).ToList();

    public IReadOnlyList<SurveyControlPresetDescriptor> GetPresets()
        => _presets.Values.ToList();

    public SurveyControlDescriptor GetControl(string controlType)
        => !string.IsNullOrEmpty(controlType) && _controls.TryGetValue(controlType, out var item) ? item.Descriptor : null;

    public SurveyControlPresetDescriptor GetPreset(string name)
        => !string.IsNullOrEmpty(name) && _presets.TryGetValue(name, out var preset) ? preset : null;

    public ISurveyControlHandler GetHandler(string controlType)
        => !string.IsNullOrEmpty(controlType) && _controls.TryGetValue(controlType, out var item) ? item.Handler : null;

    public ISurveyControlScorer GetScorer(string controlType)
        => null;

    public ISurveyControlAnalyzer GetAnalyzer(string controlType)
        => null;

    private void Add(string name, SurveyControlCapabilities capabilities, DataFieldType formulaType = DataFieldType.Text)
        => _controls[name] = (new SurveyControlDescriptor { Name = name, Capabilities = capabilities }, new BanksFakeControlHandler(name, formulaType));
}
