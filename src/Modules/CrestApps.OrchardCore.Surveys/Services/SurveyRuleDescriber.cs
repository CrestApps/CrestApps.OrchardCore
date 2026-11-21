using CrestApps.OrchardCore.Conditions;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Describes rules in plain words for the logic overview.
/// </summary>
internal sealed class SurveyRuleDescriber
{
    private readonly SurveyDefinition _definition;
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyRuleDescriber"/> class.
    /// </summary>
    /// <param name="definition">The definition the rules belong to.</param>
    /// <param name="localizer">The localizer.</param>
    public SurveyRuleDescriber(SurveyDefinition definition, IStringLocalizer localizer)
    {
        _definition = definition;
        S = localizer;
    }

    /// <summary>
    /// Describes a rule.
    /// </summary>
    /// <param name="rule">The rule.</param>
    /// <returns>The description.</returns>
    public string Describe(ConditionRule rule)
    {
        if (ConditionRule.IsEmpty(rule))
        {
            return S["always"];
        }

        var parts = rule.Conditions
            .Where(condition => condition is not null && !string.IsNullOrWhiteSpace(condition.Source))
            .Select(Describe)
            .ToList();

        return string.Join(rule.Match == ConditionMatch.Any ? S[" or "] : S[" and "], parts);
    }

    private string Describe(Condition condition)
    {
        var (source, question) = SourceLabel(condition.Source);
        var values = string.Join(", ", (condition.Values ?? []).Select(value => ValueLabel(question, value)));

        return condition.Operator switch
        {
            ConditionOperator.Is => S["{0} is {1}", source, values],
            ConditionOperator.IsNot => S["{0} is not {1}", source, values],
            ConditionOperator.IsOneOf => S["{0} is one of {1}", source, values],
            ConditionOperator.IsNoneOf => S["{0} is none of {1}", source, values],
            ConditionOperator.IsEmpty => S["{0} is not answered", source],
            ConditionOperator.IsNotEmpty => S["{0} is answered", source],
            ConditionOperator.Contains => S["{0} contains {1}", source, values],
            ConditionOperator.GreaterThan => S["{0} is greater than {1}", source, values],
            ConditionOperator.LessThan => S["{0} is less than {1}", source, values],
            _ => S["{0} {1} {2}", source, condition.Operator.ToString(), values],
        };
    }

    private (string Label, SurveyQuestion Question) SourceLabel(string source)
    {
        if (source.StartsWith("var:", StringComparison.Ordinal))
        {
            return (S["the variable {0}", source.Substring(4)], null);
        }

        if (source.StartsWith("score", StringComparison.Ordinal))
        {
            return (S["the score"], null);
        }

        var parts = source.Split('.', 2);
        var question = SurveyDefinitions.FindQuestion(_definition, parts[0]);

        if (question is null)
        {
            return (S["a removed question"], null);
        }

        var label = string.IsNullOrWhiteSpace(question.Title) ? question.Name : question.Title;

        if (parts.Length == 2 && question.Rows.FirstOrDefault(row => row.Id == parts[1]) is { } row)
        {
            label = $"{label} ({row.Text})";
        }

        return ($"“{label}”", question);
    }

    private static string ValueLabel(SurveyQuestion question, string value)
        => question?.Choices.FirstOrDefault(choice => choice.Id == value)?.Text ?? value;
}
