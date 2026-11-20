using System.Globalization;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.Learning.Assessments.Services;

/// <summary>
/// Reads the answer to one attempt item from a posted form. Field names: <c>a{index}</c> for choices, texts and numbers,
/// <c>a{index}_b{blank}</c> for blanks (a text, or the id of the choice put in it), <c>a{index}_p{promptId}</c> for
/// matches, <c>a{index}_o{position}</c> for an order. A description has nothing to answer.
/// </summary>
public static class AttemptFormReader
{
    /// <summary>
    /// Gets whether the form holds an answer to the item.
    /// </summary>
    /// <param name="form">The form.</param>
    /// <param name="item">The item.</param>
    /// <returns><see langword="true"/> when a field of the item was posted.</returns>
    public static bool HasAnswer(IFormCollection form, AttemptItem item)
    {
        if (item.QuestionType == AssessmentsConstants.QuestionTypes.Description)
        {
            return false;
        }

        var prefix = "a" + item.Index.ToString(CultureInfo.InvariantCulture);

        return form.Keys.Any(key => key == prefix || key.StartsWith(prefix + "_", StringComparison.Ordinal));
    }

    /// <summary>
    /// Reads the answer.
    /// </summary>
    /// <param name="form">The form.</param>
    /// <param name="item">The item.</param>
    /// <returns>The answer.</returns>
    public static AttemptResponse Read(IFormCollection form, AttemptItem item)
    {
        var name = "a" + item.Index.ToString(CultureInfo.InvariantCulture);
        var response = new AttemptResponse();
        var valid = item.Choices.Select(choice => choice.Id).ToHashSet(StringComparer.Ordinal);

        switch (item.QuestionType)
        {
            case AssessmentsConstants.QuestionTypes.SingleChoice:
            case AssessmentsConstants.QuestionTypes.TrueFalse:
            case AssessmentsConstants.QuestionTypes.MultipleChoice:
            case AssessmentsConstants.QuestionTypes.Survey:
                response.ChoiceIds = form[name].Where(value => value is not null && valid.Contains(value)).Distinct(StringComparer.Ordinal).ToList();
                break;

            case AssessmentsConstants.QuestionTypes.Numeric:
                response.Number = decimal.TryParse(form[name].ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var number)
                    || decimal.TryParse(form[name].ToString(), NumberStyles.Number, CultureInfo.CurrentCulture, out number) ? number : null;
                break;

            case AssessmentsConstants.QuestionTypes.FillInBlanks:
                for (var blank = 0; blank < item.BlankCount; blank++)
                {
                    response.Texts.Add(Limit(form[name + "_b" + blank.ToString(CultureInfo.InvariantCulture)].ToString()));
                }

                break;

            case AssessmentsConstants.QuestionTypes.DragIntoText:
            case AssessmentsConstants.QuestionTypes.GapSelect:
                for (var blank = 0; blank < item.BlankCount; blank++)
                {
                    var value = form[name + "_b" + blank.ToString(CultureInfo.InvariantCulture)].ToString();
                    response.ChoiceIds.Add(valid.Contains(value) ? value : string.Empty);
                }

                break;

            case AssessmentsConstants.QuestionTypes.Description:
                break;

            case AssessmentsConstants.QuestionTypes.Matching:
                var answers = item.Answers.Select(answer => answer.Id).ToHashSet(StringComparer.Ordinal);

                foreach (var prompt in item.Choices)
                {
                    var value = form[name + "_p" + prompt.Id].ToString();

                    if (answers.Contains(value))
                    {
                        response.Pairs[prompt.Id] = value;
                    }
                }

                break;

            case AssessmentsConstants.QuestionTypes.Ordering:
                for (var position = 0; position < item.Choices.Count; position++)
                {
                    var value = form[name + "_o" + position.ToString(CultureInfo.InvariantCulture)].ToString();
                    response.ChoiceIds.Add(valid.Contains(value) ? value : string.Empty);
                }

                break;

            default:
                response.Texts.Add(Limit(form[name].ToString(), 20_000));
                break;
        }

        return response;
    }

    private static string Limit(string value, int length = 500)
        => value is null ? string.Empty : value.Length > length ? value[..length] : value;
}
