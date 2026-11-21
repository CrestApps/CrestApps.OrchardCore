using System.Globalization;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Controls;

/// <summary>
/// How answers are formatted as text.
/// </summary>
public sealed class SurveyFormatContext
{
    /// <summary>
    /// Gets or sets the culture numbers and dates are written in.
    /// </summary>
    public CultureInfo Culture { get; set; } = CultureInfo.InvariantCulture;

    /// <summary>
    /// Gets or sets the definition the answers belong to.
    /// </summary>
    public SurveyDefinition Definition { get; set; }

    /// <summary>
    /// Gets or sets whether the text is for a machine export: choice values instead of texts, invariant numbers.
    /// </summary>
    public bool UseValues { get; set; }

    /// <summary>
    /// Gets or sets how a translated text is read: the text key, then the original text. <see langword="null"/>
    /// returns the original.
    /// </summary>
    public Func<string, string, string> Translate { get; set; }

    /// <summary>
    /// Gets a text in the context's culture.
    /// </summary>
    /// <param name="key">The text key.</param>
    /// <param name="text">The original text.</param>
    /// <returns>The translated or original text.</returns>
    public string Text(string key, string text)
        => Translate?.Invoke(key, text) ?? text;
}
