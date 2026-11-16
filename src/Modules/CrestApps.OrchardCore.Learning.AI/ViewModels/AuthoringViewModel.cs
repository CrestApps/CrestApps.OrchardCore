using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Learning.AI.ViewModels;

/// <summary>
/// The authoring assistant page.
/// </summary>
public class AuthoringViewModel
{
    /// <summary>
    /// Gets or sets whether an AI deployment is set up.
    /// </summary>
    public bool ModelAvailable { get; set; }

    /// <summary>
    /// Gets or sets whether question banks exist on the site (Learning Assessments is on).
    /// </summary>
    public bool CanDraftQuestions { get; set; }

    /// <summary>
    /// Gets or sets whether rubrics exist on the site (Learning Coursework is on).
    /// </summary>
    public bool CanDraftRubrics { get; set; }

    /// <summary>
    /// Gets or sets the recently changed lessons to pick from.
    /// </summary>
    public IList<SelectListItem> Lessons { get; set; } = [];

    /// <summary>
    /// Gets or sets the question banks to pick from.
    /// </summary>
    public IList<SelectListItem> Banks { get; set; } = [];

    /// <summary>
    /// Gets or sets the course topic.
    /// </summary>
    public string Topic { get; set; }

    /// <summary>
    /// Gets or sets who the course is for.
    /// </summary>
    public string Audience { get; set; }

    /// <summary>
    /// Gets or sets about how many lessons to draft.
    /// </summary>
    public int LessonCount { get; set; } = 6;

    /// <summary>
    /// Gets or sets the lesson to draft questions from or to translate.
    /// </summary>
    public string LessonId { get; set; }

    /// <summary>
    /// Gets or sets the bank the drafted questions go in.
    /// </summary>
    public string BankId { get; set; }

    /// <summary>
    /// Gets or sets how many questions to draft.
    /// </summary>
    public int QuestionCount { get; set; } = 5;

    /// <summary>
    /// Gets or sets the work the rubric grades.
    /// </summary>
    public string Task { get; set; }

    /// <summary>
    /// Gets or sets about how many criteria the rubric has.
    /// </summary>
    public int CriteriaCount { get; set; } = 4;

    /// <summary>
    /// Gets or sets the language to translate into.
    /// </summary>
    public string Language { get; set; }
}
