using System.Text.Json;
using CrestApps.OrchardCore.Conditions;
using CrestApps.OrchardCore.Surveys.Models;
using OrchardCore;

namespace CrestApps.OrchardCore.Surveys.Core.Services;

/// <summary>
/// Copies definitions and questions, and gives copies new ids while keeping every reference between them.
/// </summary>
public static class SurveyDefinitionCloner
{
    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.General);

    /// <summary>
    /// Makes a deep copy of a definition with the same ids.
    /// </summary>
    /// <param name="definition">The definition.</param>
    /// <returns>The copy.</returns>
    public static SurveyDefinition Clone(SurveyDefinition definition)
        => definition is null ? null : JsonSerializer.Deserialize<SurveyDefinition>(JsonSerializer.Serialize(definition, _options), _options);

    /// <summary>
    /// Makes a deep copy of a question with the same ids.
    /// </summary>
    /// <param name="question">The question.</param>
    /// <returns>The copy.</returns>
    public static SurveyQuestion Clone(SurveyQuestion question)
        => question is null ? null : JsonSerializer.Deserialize<SurveyQuestion>(JsonSerializer.Serialize(question, _options), _options);

    /// <summary>
    /// Gives every page, question, choice, row, pool, branch, ending, category, band and quota of a definition a new id,
    /// and rewrites every reference to the old ids.
    /// </summary>
    /// <param name="definition">The definition, changed in place.</param>
    /// <returns>The map from old to new ids.</returns>
    public static IReadOnlyDictionary<string, string> RegenerateIds(SurveyDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var map = new Dictionary<string, string>(StringComparer.Ordinal);

        string Next(string id)
        {
            var next = IdGenerator.GenerateId();

            if (!string.IsNullOrEmpty(id))
            {
                map[id] = next;
            }

            return next;
        }

        foreach (var page in definition.Pages)
        {
            page.Id = Next(page.Id);

            foreach (var pool in page.Pools)
            {
                pool.Id = Next(pool.Id);
            }

            foreach (var branch in page.Branches)
            {
                branch.Id = Next(branch.Id);
            }

            foreach (var question in page.Questions)
            {
                AssignNewIds(question, Next);
            }
        }

        foreach (var ending in definition.Endings)
        {
            ending.Id = Next(ending.Id);
        }

        foreach (var category in definition.ScoreCategories)
        {
            category.Id = Next(category.Id);
        }

        foreach (var band in definition.ScoreBands)
        {
            band.Id = Next(band.Id);
        }

        foreach (var quota in definition.Quotas)
        {
            quota.Id = Next(quota.Id);
        }

        RemapReferences(definition, map);

        return map;
    }

    /// <summary>
    /// Gives a question and its choices and rows new ids, and rewrites the references inside the question.
    /// </summary>
    /// <param name="question">The question, changed in place.</param>
    /// <returns>The map from old to new ids.</returns>
    public static IReadOnlyDictionary<string, string> RegenerateIds(SurveyQuestion question)
    {
        ArgumentNullException.ThrowIfNull(question);

        var map = new Dictionary<string, string>(StringComparer.Ordinal);

        AssignNewIds(question, id =>
        {
            var next = IdGenerator.GenerateId();

            if (!string.IsNullOrEmpty(id))
            {
                map[id] = next;
            }

            return next;
        });

        RemapQuestion(question, map);

        return map;
    }

    private static void AssignNewIds(SurveyQuestion question, Func<string, string> next)
    {
        question.Id = next(question.Id);

        foreach (var choice in question.Choices)
        {
            choice.Id = next(choice.Id);
        }

        foreach (var row in question.Rows)
        {
            row.Id = next(row.Id);
        }
    }

    private static void RemapReferences(SurveyDefinition definition, Dictionary<string, string> map)
    {
        foreach (var page in definition.Pages)
        {
            RemapVisibility(page.Visibility, map);

            foreach (var branch in page.Branches)
            {
                RemapRule(branch.Rule, map);
                branch.TargetId = Map(branch.TargetId, map);
            }

            foreach (var question in page.Questions)
            {
                question.PoolId = Map(question.PoolId, map);
                RemapQuestion(question, map);
            }
        }

        foreach (var ending in definition.Endings)
        {
            RemapVisibility(ending.Condition, map);
        }

        foreach (var quota in definition.Quotas)
        {
            RemapRule(quota.Rule, map);
            quota.EndingId = Map(quota.EndingId, map);
        }
    }

    private static void RemapQuestion(SurveyQuestion question, IReadOnlyDictionary<string, string> map)
    {
        RemapVisibility(question.Visibility, map);
        RemapRule(question.RequiredWhen, map);
        RemapRule(question.EnabledWhen, map);
        question.ChoicesFrom = Map(question.ChoicesFrom, map);

        foreach (var choice in question.Choices)
        {
            RemapVisibility(choice.Visibility, map);
        }

        var scoring = question.Scoring;

        if (scoring is null)
        {
            return;
        }

        scoring.CategoryId = Map(scoring.CategoryId, map);
        scoring.ChoicePoints = scoring.ChoicePoints.ToDictionary(pair => Map(pair.Key, map), pair => pair.Value, StringComparer.Ordinal);
        scoring.CorrectChoiceIds = scoring.CorrectChoiceIds.Select(id => Map(id, map)).ToList();
        scoring.Pairs = scoring.Pairs.ToDictionary(pair => Map(pair.Key, map), pair => Map(pair.Value, map), StringComparer.Ordinal);
        scoring.ChoiceFeedback = scoring.ChoiceFeedback.ToDictionary(pair => Map(pair.Key, map), pair => pair.Value, StringComparer.Ordinal);
    }

    private static void RemapVisibility(SurveyVisibility visibility, IReadOnlyDictionary<string, string> map)
    {
        if (visibility is not null)
        {
            RemapRule(visibility.Rule, map);
        }
    }

    private static void RemapRule(ConditionRule rule, IReadOnlyDictionary<string, string> map)
    {
        if (rule is null)
        {
            return;
        }

        foreach (var condition in rule.Conditions)
        {
            if (condition is null)
            {
                continue;
            }

            condition.Source = MapSource(condition.Source, map);
            condition.Values = condition.Values?.Select(value => Map(value, map)).ToList() ?? [];
        }

        foreach (var group in rule.Groups ?? [])
        {
            RemapRule(group, map);
        }
    }

    private static string MapSource(string source, IReadOnlyDictionary<string, string> map)
    {
        if (string.IsNullOrEmpty(source) || source.Contains(':'))
        {
            return source;
        }

        var dot = source.IndexOf('.');

        if (dot < 0)
        {
            return Map(source, map);
        }

        return Map(source.Substring(0, dot), map) + "." + Map(source.Substring(dot + 1), map);
    }

    private static string Map(string id, IReadOnlyDictionary<string, string> map)
        => id is not null && map.TryGetValue(id, out var next) ? next : id;
}
