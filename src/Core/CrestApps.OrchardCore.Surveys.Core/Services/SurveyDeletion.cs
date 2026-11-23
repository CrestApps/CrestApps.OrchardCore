namespace CrestApps.OrchardCore.Surveys.Core.Services;

/// <summary>
/// Remembers a survey delete for the upkeep: written with the survey's deleting mark (<see cref="Models.Survey.DeletingUtc"/>),
/// before its records are removed. While the survey still exists, the upkeep finishes a delete that stopped half way;
/// once it is gone, the upkeep removes what a send, a reminder or a response that was already running wrote for it
/// after the delete, and forgets the delete after <see cref="SurveyManager.DeletionFollowUp"/>.
/// </summary>
public sealed class SurveyDeletion
{
    /// <summary>
    /// Gets or sets the document id.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the id of the survey.
    /// </summary>
    public string SurveyId { get; set; }

    /// <summary>
    /// Gets or sets when the delete began, in UTC.
    /// </summary>
    public DateTime StartedUtc { get; set; }
}
