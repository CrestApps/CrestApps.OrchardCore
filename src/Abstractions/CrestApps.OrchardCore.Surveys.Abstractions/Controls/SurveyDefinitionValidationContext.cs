using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Controls;

/// <summary>
/// The state of a definition check: the definition and the problems found.
/// </summary>
public sealed class SurveyDefinitionValidationContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyDefinitionValidationContext"/> class.
    /// </summary>
    /// <param name="survey">The survey, or <see langword="null"/> for a bank question.</param>
    /// <param name="definition">The definition checked.</param>
    public SurveyDefinitionValidationContext(Survey survey, SurveyDefinition definition)
    {
        Survey = survey;
        Definition = definition;
    }

    /// <summary>
    /// Gets the survey, or <see langword="null"/>.
    /// </summary>
    public Survey Survey { get; }

    /// <summary>
    /// Gets the definition checked.
    /// </summary>
    public SurveyDefinition Definition { get; }

    /// <summary>
    /// Gets the problems found.
    /// </summary>
    public List<SurveyValidationMessage> Messages { get; } = [];

    /// <summary>
    /// Gets whether no error was found.
    /// </summary>
    public bool Succeeded => !Messages.Any(message => message.IsError);

    /// <summary>
    /// Adds an error that blocks publishing.
    /// </summary>
    /// <param name="code">A stable code.</param>
    /// <param name="message">The localized message.</param>
    /// <param name="questionId">The question concerned, or <see langword="null"/>.</param>
    /// <param name="pageId">The page concerned, or <see langword="null"/>.</param>
    public void AddError(string code, string message, string questionId = null, string pageId = null)
        => Messages.Add(new SurveyValidationMessage { IsError = true, Code = code, Message = message, QuestionId = questionId, PageId = pageId });

    /// <summary>
    /// Adds a warning shown before publishing.
    /// </summary>
    /// <param name="code">A stable code.</param>
    /// <param name="message">The localized message.</param>
    /// <param name="questionId">The question concerned, or <see langword="null"/>.</param>
    /// <param name="pageId">The page concerned, or <see langword="null"/>.</param>
    public void AddWarning(string code, string message, string questionId = null, string pageId = null)
        => Messages.Add(new SurveyValidationMessage { IsError = false, Code = code, Message = message, QuestionId = questionId, PageId = pageId });
}
