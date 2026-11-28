using System.Globalization;
using System.Net;
using System.Text;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;

namespace CrestApps.OrchardCore.Surveys.Core.Logic;

/// <summary>
/// Replaces <c>{name}</c> references in texts with answers, variables and scores, HTML-encoded. Unknown names render
/// empty. In HTML mode the braces of a value are encoded too, so a piped answer never reads as a reference, or as
/// Liquid, in whatever processes the text next.
/// </summary>
public sealed class SurveyPipingRenderer
{
    private readonly ISurveyControlRegistry _registry;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyPipingRenderer"/> class.
    /// </summary>
    /// <param name="registry">The control registry.</param>
    public SurveyPipingRenderer(ISurveyControlRegistry registry)
    {
        _registry = registry;
    }

    /// <summary>
    /// Gets whether a text has a piping reference.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns><see langword="true"/> when it has one.</returns>
    public static bool HasReferences(string text)
        => !string.IsNullOrEmpty(text) && text.Contains('{', StringComparison.Ordinal) && text.Contains('}', StringComparison.Ordinal);

    /// <summary>
    /// Lists the names a text refers to.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The names, as written between braces.</returns>
    public static IEnumerable<string> References(string text)
    {
        if (!HasReferences(text))
        {
            yield break;
        }

        var start = -1;

        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '{')
            {
                start = i;
            }
            else if (text[i] == '}' && start >= 0)
            {
                var name = text.Substring(start + 1, i - start - 1).Trim();

                if (name.Length > 0 && name.Length <= 200 && !name.Contains('{', StringComparison.Ordinal))
                {
                    yield return name;
                }

                start = -1;
            }
        }
    }

    /// <summary>
    /// Renders a text with its references replaced.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="definition">The definition.</param>
    /// <param name="response">The response.</param>
    /// <param name="hiddenQuestionIds">The ids of hidden questions, which render empty, or <see langword="null"/>.</param>
    /// <param name="encode">Whether values are HTML-encoded (for HTML texts) or URL-encoded when <paramref name="urlEncode"/> is set.</param>
    /// <param name="urlEncode">Whether values are URL-encoded (for redirect URLs).</param>
    /// <param name="showScore">Whether the score may show: when not, <c>{score}</c>, <c>{percent}</c> and
    /// <c>{score:category}</c> render empty.</param>
    /// <returns>The rendered text.</returns>
    public string Render(string text, SurveyDefinition definition, SurveyResponse response, ISet<string> hiddenQuestionIds = null, bool encode = true, bool urlEncode = false, bool showScore = true)
    {
        if (!HasReferences(text) || definition is null || response is null)
        {
            return text;
        }

        var format = new SurveyFormatContext
        {
            Culture = CultureFor(response.Culture),
            Definition = definition,
        };

        var builder = new StringBuilder(text.Length);
        var start = -1;
        var last = 0;

        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '{')
            {
                start = i;
            }
            else if (text[i] == '}' && start >= 0)
            {
                var name = text.Substring(start + 1, i - start - 1).Trim();

                if (name.Length is > 0 and <= 200 && !name.Contains('{', StringComparison.Ordinal) && !name.Contains(' ', StringComparison.Ordinal))
                {
                    builder.Append(text, last, start - last);
                    var value = !showScore && IsScoreReference(name) ? string.Empty : Resolve(name, definition, response, hiddenQuestionIds, format) ?? string.Empty;
                    builder.Append(urlEncode ? Uri.EscapeDataString(value) : encode ? HtmlEncode(value) : value);
                    last = i + 1;
                }

                start = -1;
            }
        }

        builder.Append(text, last, text.Length - last);

        return builder.ToString();
    }

    /// <summary>
    /// HTML-encodes a piped value, with its braces encoded as well.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The encoded value.</returns>
    public static string HtmlEncode(string value)
        => WebUtility.HtmlEncode(value ?? string.Empty)
            .Replace("{", "&#123;", StringComparison.Ordinal)
            .Replace("}", "&#125;", StringComparison.Ordinal);

    /// <summary>
    /// Gets whether a reference names the score: <c>score</c>, <c>percent</c> or <c>score:category</c>.
    /// </summary>
    /// <param name="name">The reference, without its braces.</param>
    /// <returns><see langword="true"/> for a score reference.</returns>
    public static bool IsScoreReference(string name)
        => string.Equals(name, "score", StringComparison.Ordinal)
            || string.Equals(name, "percent", StringComparison.Ordinal)
            || (name?.StartsWith("score:", StringComparison.Ordinal) ?? false);

    private string Resolve(string name, SurveyDefinition definition, SurveyResponse response, ISet<string> hidden, SurveyFormatContext format)
    {
        if (string.Equals(name, "score", StringComparison.Ordinal))
        {
            return response.Score?.ToString("0.##", format.Culture);
        }

        if (string.Equals(name, "percent", StringComparison.Ordinal))
        {
            return response.Percent?.ToString("0.#", format.Culture);
        }

        if (name.StartsWith("score:", StringComparison.Ordinal))
        {
            var categoryName = name.Substring(6);
            var category = definition.ScoreCategories?.FirstOrDefault(item => string.Equals(item.Name, categoryName, StringComparison.Ordinal) || string.Equals(item.Id, categoryName, StringComparison.Ordinal));

            return category is not null && response.CategoryScores.TryGetValue(category.Id, out var score)
                ? score.ToString("0.##", format.Culture)
                : null;
        }

        if (name.StartsWith("var:", StringComparison.Ordinal))
        {
            name = name.Substring(4);

            return response.Variables.TryGetValue(name, out var variable) ? variable : null;
        }

        var questionName = name;
        string rowName = null;
        var dot = name.IndexOf('.', StringComparison.Ordinal);

        if (dot > 0)
        {
            questionName = name.Substring(0, dot);
            rowName = name.Substring(dot + 1);
        }

        var question = definition.Pages?
            .SelectMany(page => page.Questions ?? [])
            .FirstOrDefault(item => string.Equals(item.Name, questionName, StringComparison.Ordinal));

        if (question is null)
        {
            // Variables may be referred to without the var: prefix: names are unique across questions and variables.
            return rowName is null && response.Variables.TryGetValue(name, out var value) ? value : null;
        }

        if (hidden is not null && hidden.Contains(question.Id))
        {
            return null;
        }

        var handler = _registry.GetHandler(question.ControlType);

        if (handler is null || !response.Answers.TryGetValue(question.Id, out var answer))
        {
            return null;
        }

        if (rowName is null)
        {
            return handler.FormatAnswer(question, answer, format);
        }

        var row = question.Rows?.FirstOrDefault(item => string.Equals(item.Name, rowName, StringComparison.Ordinal) || string.Equals(item.Id, rowName, StringComparison.Ordinal));

        if (row is null || answer.Rows is null || !answer.Rows.TryGetValue(row.Id, out var values))
        {
            return null;
        }

        var rowAnswer = new SurveyAnswer { Values = values };

        return handler.FormatAnswer(question, rowAnswer, format);
    }

    private static CultureInfo CultureFor(string culture)
    {
        if (string.IsNullOrEmpty(culture))
        {
            return CultureInfo.CurrentCulture;
        }

        try
        {
            return CultureInfo.GetCultureInfo(culture);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.CurrentCulture;
        }
    }
}
