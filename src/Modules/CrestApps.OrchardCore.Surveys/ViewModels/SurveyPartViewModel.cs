using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// An embedded survey as respondents see it.
/// </summary>
public class SurveyPartViewModel
{
    /// <summary>
    /// Gets or sets the survey title respondents see.
    /// </summary>
    [BindNever]
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the sanitized introduction.
    /// </summary>
    [BindNever]
    public IHtmlContent Introduction { get; set; }

    /// <summary>
    /// Gets or sets the collector token, or <see langword="null"/> when the survey cannot be answered.
    /// </summary>
    [BindNever]
    public string Token { get; set; }

    /// <summary>
    /// Gets or sets how the survey shows.
    /// </summary>
    [BindNever]
    public SurveyEmbedDisplay Display { get; set; }

    /// <summary>
    /// Gets or sets the text of the button or link.
    /// </summary>
    [BindNever]
    public string Text { get; set; }
}
