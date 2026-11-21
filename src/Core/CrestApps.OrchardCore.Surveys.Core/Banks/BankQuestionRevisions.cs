using System.Globalization;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Core.Banks;

/// <summary>
/// Reads and writes the bank revision a published version records on each question it copied from a bank, under
/// <c>Properties["bankRevision"]</c>.
/// </summary>
public static class BankQuestionRevisions
{
    /// <summary>
    /// The property name that holds the revision.
    /// </summary>
    public const string PropertyName = "bankRevision";

    /// <summary>
    /// Gets the bank revision a question was copied at.
    /// </summary>
    /// <param name="question">The question of a version.</param>
    /// <param name="revision">The revision.</param>
    /// <returns><see langword="true"/> when the question was copied from a bank.</returns>
    public static bool TryGet(SurveyQuestion question, out int revision)
    {
        revision = 0;

        if (question?.Properties is null || !question.Properties.TryGetPropertyValue(PropertyName, out var node) || node is not JsonValue value)
        {
            return false;
        }

        if (value.TryGetValue<int>(out revision))
        {
            return true;
        }

        return value.TryGetValue<string>(out var text) && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out revision);
    }

    /// <summary>
    /// Records the bank revision a question was copied at.
    /// </summary>
    /// <param name="question">The question of a version.</param>
    /// <param name="revision">The revision.</param>
    public static void Set(SurveyQuestion question, int revision)
    {
        ArgumentNullException.ThrowIfNull(question);

        question.Properties ??= [];
        question.Properties[PropertyName] = revision;
    }
}
