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

    /// <summary>
    /// Gives the choices and rows of a question the ids of the matching choices and rows of an earlier copy of it, and
    /// rewrites the references inside the question (scoring keys, choice rules) to them. A choice matches by value, then
    /// by text, then by position; a row by name, then by text, then by position. Choices and rows of the earlier copy that
    /// match nothing are kept, retired, so answers given to them still read.
    /// </summary>
    /// <param name="question">The new copy of the question, changed in place.</param>
    /// <param name="previous">The earlier copy.</param>
    /// <returns>The map from the new copy's ids to the kept ids.</returns>
    public static IReadOnlyDictionary<string, string> KeepIds(SurveyQuestion question, SurveyQuestion previous)
    {
        ArgumentNullException.ThrowIfNull(question);

        var map = new Dictionary<string, string>(StringComparer.Ordinal);

        if (previous is null)
        {
            return map;
        }

        var keptChoices = Match(question.Choices, previous.Choices, choice => choice.Id, (choice, id) => choice.Id = id, choice => choice.Value, choice => choice.Text, map);

        foreach (var choice in keptChoices)
        {
            choice.Retired = true;
            question.Choices.Add(choice);
        }

        var keptRows = Match(question.Rows, previous.Rows, row => row.Id, (row, id) => row.Id = id, row => row.Name, row => row.Text, map);

        foreach (var row in keptRows)
        {
            row.Retired = true;
            question.Rows.Add(row);
        }

        RemapQuestion(question, map);

        return map;
    }

    // Matches incoming items to earlier ones (by key, then by text, then by position), gives each matched item the
    // earlier id, and returns the earlier items nothing matched.
    private static List<T> Match<T>(List<T> incoming, List<T> earlier, Func<T, string> id, Action<T, string> setId, Func<T, string> key, Func<T, string> text, Dictionary<string, string> map)
        where T : class
    {
        incoming ??= [];
        var left = (earlier ?? []).Where(item => item is not null && !string.IsNullOrEmpty(id(item))).ToList();
        var matched = new T[incoming.Count];

        void Pass(Func<T, string> by)
        {
            for (var i = 0; i < incoming.Count; i++)
            {
                if (matched[i] is not null || incoming[i] is null || string.IsNullOrWhiteSpace(by(incoming[i])))
                {
                    continue;
                }

                var found = left.FirstOrDefault(item => string.Equals(by(item)?.Trim(), by(incoming[i]).Trim(), StringComparison.OrdinalIgnoreCase));

                if (found is not null)
                {
                    matched[i] = found;
                    left.Remove(found);
                }
            }
        }

        Pass(key);
        Pass(text);

        // By position, only for items without a key on either side: the item at the same place, when it was not taken
        // by another. An item with a key that matched nothing is new, and the earlier one it replaces stays, retired.
        var earlierList = earlier ?? [];

        for (var i = 0; i < incoming.Count && i < earlierList.Count; i++)
        {
            if (matched[i] is null
                && incoming[i] is not null
                && earlierList[i] is not null
                && string.IsNullOrWhiteSpace(key(incoming[i]))
                && string.IsNullOrWhiteSpace(key(earlierList[i]))
                && left.Contains(earlierList[i]))
            {
                matched[i] = earlierList[i];
                left.Remove(earlierList[i]);
            }
        }

        for (var i = 0; i < incoming.Count; i++)
        {
            if (matched[i] is null)
            {
                continue;
            }

            var oldId = id(incoming[i]);
            var keptId = id(matched[i]);

            if (!string.IsNullOrEmpty(oldId))
            {
                map[oldId] = keptId;
            }

            setId(incoming[i], keptId);
        }

        return left;
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

        RemapTranslations(definition, map);
    }

    // Translation keys name items by id (title:{question}, choice:{question}:{choice}...; see SurveyTextKeys), so every
    // id segment after the kind follows its item.
    private static void RemapTranslations(SurveyDefinition definition, Dictionary<string, string> map)
    {
        if (definition.Translations is not { Count: > 0 })
        {
            return;
        }

        foreach (var culture in definition.Translations.Keys.ToList())
        {
            if (definition.Translations[culture] is not { } texts)
            {
                continue;
            }

            definition.Translations[culture] = texts.ToDictionary(pair => RemapKey(pair.Key, map), pair => pair.Value, StringComparer.Ordinal);
        }

        if (definition.TranslationSources is not { Count: > 0 })
        {
            return;
        }

        foreach (var culture in definition.TranslationSources.Keys.ToList())
        {
            if (definition.TranslationSources[culture] is not { } sources)
            {
                continue;
            }

            definition.TranslationSources[culture] = sources.ToDictionary(pair => RemapKey(pair.Key, map), pair => pair.Value, StringComparer.Ordinal);
        }
    }

    private static string RemapKey(string key, IReadOnlyDictionary<string, string> map)
    {
        if (string.IsNullOrEmpty(key) || !key.Contains(':'))
        {
            return key;
        }

        var parts = key.Split(':');

        for (var i = 1; i < parts.Length; i++)
        {
            parts[i] = Map(parts[i], map);
        }

        return string.Join(':', parts);
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
        if (string.IsNullOrEmpty(source))
        {
            return source;
        }

        // A category score source names the category by id (or by name, which stays).
        if (source.StartsWith("score:", StringComparison.Ordinal))
        {
            return "score:" + Map(source.Substring(6), map);
        }

        if (source.Contains(':'))
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
