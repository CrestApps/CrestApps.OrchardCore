namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// A request to check one answer with immediate feedback.
/// </summary>
public sealed class CheckAnswerRequest : SubmitPageRequest
{
    /// <summary>
    /// Gets or sets the id of the question checked.
    /// </summary>
    public string QuestionId { get; set; }
}
