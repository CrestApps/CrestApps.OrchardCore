using CrestApps.OrchardCore.Conditions;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Tests.Surveys.TestSupport;

/// <summary>
/// Builds survey definitions for runtime tests.
/// </summary>
internal static class RuntimeSurveys
{
    public static SurveyQuestion Text(string id, bool required = false)
        => new() { Id = id, Name = id, ControlType = RuntimeTextControl.Name, Title = id, Required = required };

    public static SurveyQuestion Number(string id, bool required = false)
        => new() { Id = id, Name = id, ControlType = RuntimeNumberControl.Name, Title = id, Required = required };

    public static SurveyQuestion Choice(string id, params string[] choices)
        => new()
        {
            Id = id,
            Name = id,
            ControlType = RuntimeChoiceControl.Name,
            Title = id,
            Choices = choices.Select(choice => new SurveyChoice { Id = choice, Value = choice, Text = choice.ToUpperInvariant() }).ToList(),
        };

    public static SurveyPage Page(string id, params SurveyQuestion[] questions)
        => new() { Id = id, Name = id, Title = id, Questions = [.. questions] };

    public static SurveyDefinition Definition(params SurveyPage[] pages)
        => new()
        {
            Title = "Test",
            Pages = [.. pages],
            Endings = [new SurveyEnding { Id = "end", Name = "end", Title = "Thanks", Message = "Thanks {q1}", IsDefault = true }],
        };

    public static SurveyVisibility ShowWhen(string source, ConditionOperator op, params string[] values)
        => new()
        {
            Mode = VisibilityMode.ShowWhen,
            Rule = new ConditionRule { Conditions = [new Condition { Source = source, Operator = op, Values = [.. values] }] },
        };

    public static SurveyVisibility HideWhen(string source, ConditionOperator op, params string[] values)
        => new()
        {
            Mode = VisibilityMode.HideWhen,
            Rule = new ConditionRule { Conditions = [new Condition { Source = source, Operator = op, Values = [.. values] }] },
        };
}
