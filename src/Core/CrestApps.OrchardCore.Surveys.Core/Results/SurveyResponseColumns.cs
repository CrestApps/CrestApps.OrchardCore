using System.Text.Json;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Core.Controls;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;

namespace CrestApps.OrchardCore.Surveys.Core.Results;

/// <summary>
/// The export columns of a survey across its versions, joined by question id: the newest version names each question,
/// and questions only older versions had follow. Each response writes its answers under these columns, whatever the
/// question was called in the version it ran on.
/// </summary>
public sealed class SurveyResponseColumns
{
    private readonly ISurveyControlRegistry _registry;
    private readonly Dictionary<string, string> _canonicalNames = new(StringComparer.Ordinal);
    private readonly Dictionary<(string VersionId, string QuestionId), SurveyQuestion> _renamed = [];

    private SurveyResponseColumns(ISurveyControlRegistry registry)
    {
        _registry = registry;
    }

    /// <summary>
    /// Gets the columns in order.
    /// </summary>
    public IReadOnlyList<SurveyExportColumn> Columns { get; private set; } = [];

    /// <summary>
    /// Gets the titles of the questions that have no column because their question type is turned off, so a report can
    /// say so instead of dropping them silently.
    /// </summary>
    public IReadOnlyList<string> MissingQuestions { get; private set; } = [];

    /// <summary>
    /// Builds the columns of a survey's versions.
    /// </summary>
    /// <param name="versions">The versions, in any order.</param>
    /// <param name="registry">The control registry.</param>
    /// <returns>The columns.</returns>
    public static SurveyResponseColumns Build(IEnumerable<SurveyVersion> versions, ISurveyControlRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(versions);
        ArgumentNullException.ThrowIfNull(registry);

        var result = new SurveyResponseColumns(registry);
        var columns = new List<SurveyExportColumn>();
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var missing = new List<string>();
        var missingIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var version in versions.Where(version => version?.Definition is not null).OrderByDescending(version => version.Number))
        {
            foreach (var (_, question) in SurveyDefinitionQuestions.All(version.Definition))
            {
                if (result._canonicalNames.ContainsKey(question.Id))
                {
                    continue;
                }

                var handler = registry.GetHandler(question.ControlType);

                if (handler is null)
                {
                    if (missingIds.Add(question.Id))
                    {
                        missing.Add(string.IsNullOrWhiteSpace(question.Title) ? question.Name ?? question.Id : question.Title);
                    }

                    continue;
                }

                if (registry.GetControl(question.ControlType) is { } descriptor && !descriptor.Has(SurveyControlCapabilities.Answerable))
                {
                    continue;
                }

                // A name an older question shares with a newer one gets a number, so every column key stays unique.
                var name = string.IsNullOrWhiteSpace(question.Name) ? question.Id : question.Name;
                var unique = name;

                for (var suffix = 2; !names.Add(unique); suffix++)
                {
                    unique = $"{name}_{suffix}";
                }

                result._canonicalNames[question.Id] = unique;

                foreach (var column in handler.DescribeColumns(Rename(question, unique, version.Definition)) ?? [])
                {
                    if (column is not null && !string.IsNullOrEmpty(column.Key) && keys.Add(column.Key))
                    {
                        column.QuestionId ??= question.Id;
                        columns.Add(column);
                    }
                }
            }
        }

        result.Columns = columns;
        result.MissingQuestions = missing;

        return result;
    }

    /// <summary>
    /// Writes the answers of a response under the columns.
    /// </summary>
    /// <param name="response">The response.</param>
    /// <param name="definition">The definition of the version the response ran on.</param>
    /// <param name="row">The row, by column key.</param>
    /// <param name="context">The format context.</param>
    public void Write(SurveyResponse response, SurveyDefinition definition, IDictionary<string, object> row, SurveyFormatContext context)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(row);

        if (definition is null)
        {
            return;
        }

        foreach (var (_, question) in SurveyDefinitionQuestions.All(definition))
        {
            if (!_canonicalNames.TryGetValue(question.Id, out var name) || _registry.GetHandler(question.ControlType) is not { } handler)
            {
                continue;
            }

            if (!_renamed.TryGetValue((response.SurveyVersionId ?? string.Empty, question.Id), out var renamed))
            {
                _renamed[(response.SurveyVersionId ?? string.Empty, question.Id)] = renamed = Rename(question, name, definition);
            }

            response.Answers.TryGetValue(question.Id, out var answer);
            handler.WriteColumns(renamed, answer, row, context);
        }
    }

    // The question under its column name, with the choices it offers (carried-forward choices are those of the question
    // they come from), so its columns and values name those choices.
    private static SurveyQuestion Rename(SurveyQuestion question, string name, SurveyDefinition definition)
    {
        var resolved = SurveyChoiceSource.WithSourceChoices(question, definition);

        if (string.Equals(resolved.Name, name, StringComparison.Ordinal))
        {
            return resolved;
        }

        var copy = ReferenceEquals(resolved, question) ? JsonSerializer.Deserialize<SurveyQuestion>(JsonSerializer.Serialize(question)) : resolved;
        copy.Name = name;

        return copy;
    }
}
