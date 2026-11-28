using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Surveys.AI.ViewModels;

/// <summary>
/// The page that summarizes the written answers to a question of a survey.
/// </summary>
public class SurveyAISummarizeViewModel
{
    /// <summary>
    /// Gets or sets the survey.
    /// </summary>
    public Survey Survey { get; set; }

    /// <summary>
    /// Gets or sets the written questions of the published version.
    /// </summary>
    public List<SelectListItem> Questions { get; set; } = [];

    /// <summary>
    /// Gets or sets the question summarized.
    /// </summary>
    public string QuestionId { get; set; }

    /// <summary>
    /// Gets or sets the summary, in Markdown.
    /// </summary>
    public string Summary { get; set; }

    /// <summary>
    /// Gets or sets how many answers were read.
    /// </summary>
    public int AnswerCount { get; set; }

    /// <summary>
    /// Gets or sets whether an AI model is set up.
    /// </summary>
    public bool ModelAvailable { get; set; }
}
