using CrestApps.Core.Models;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Records;

namespace CrestApps.OrchardCore.Surveys.Models;

/// <summary>
/// A response to a survey (an attempt of a quiz), pinned to the version it started on. It stores answers only.
/// </summary>
public sealed class SurveyResponse : CatalogItem
{
    /// <summary>
    /// Gets or sets the id of the survey.
    /// </summary>
    public string SurveyId { get; set; }

    /// <summary>
    /// Gets or sets the id of the version it started on.
    /// </summary>
    public string SurveyVersionId { get; set; }

    /// <summary>
    /// Gets or sets the number of that version.
    /// </summary>
    public int VersionNumber { get; set; }

    /// <summary>
    /// Gets or sets the readable number, such as R0001234.
    /// </summary>
    public string Number { get; set; }

    /// <summary>
    /// Gets or sets where the response stands.
    /// </summary>
    public SurveyResponseStatus Status { get; set; }

    /// <summary>
    /// Gets or sets where grading stands.
    /// </summary>
    public GradingStatus Grading { get; set; }

    /// <summary>
    /// Gets or sets the id of the user who responds, or null when anonymous or not signed in.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the party who responds, or null when anonymous.
    /// </summary>
    public PartyRef? Respondent { get; set; }

    /// <summary>
    /// Gets or sets the id of the collector it came through.
    /// </summary>
    public string CollectorId { get; set; }

    /// <summary>
    /// Gets or sets the id of the invitation it came from, or null.
    /// </summary>
    public string InvitationId { get; set; }

    /// <summary>
    /// Gets or sets what the response belongs to, such as an enrollment or a ticket.
    /// </summary>
    public RecordRef? Context { get; set; }

    /// <summary>
    /// Gets or sets the attempt number of the respondent in this context.
    /// </summary>
    public int AttemptNumber { get; set; }

    /// <summary>
    /// Gets or sets the culture it is presented in.
    /// </summary>
    public string Culture { get; set; }

    /// <summary>
    /// Gets or sets whether it is a preview or test response, left out of results and limits.
    /// </summary>
    public bool IsTest { get; set; }

    /// <summary>
    /// Gets or sets when it started, in UTC.
    /// </summary>
    public DateTime StartedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the respondent last saved, in UTC.
    /// </summary>
    public DateTime LastActivityUtc { get; set; }

    /// <summary>
    /// Gets or sets when it ended, in UTC.
    /// </summary>
    public DateTime? CompletedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the response last changed, in UTC, for incremental reads.
    /// </summary>
    public DateTime ModifiedUtc { get; set; }

    /// <summary>
    /// Gets or sets when time runs out, in UTC, or null.
    /// </summary>
    public DateTime? DeadlineUtc { get; set; }

    /// <summary>
    /// Gets or sets whether it was submitted at its deadline.
    /// </summary>
    public bool AutoSubmitted { get; set; }

    /// <summary>
    /// Gets or sets the id of the page the respondent is on.
    /// </summary>
    public string CurrentPageId { get; set; }

    /// <summary>
    /// Gets or sets the ids of the pages visited in order; going back removes the last one.
    /// </summary>
    public List<string> PagePath { get; set; } = [];

    /// <summary>
    /// Gets or sets every page visit, for the funnel and timings.
    /// </summary>
    public List<SurveyPageVisit> PageVisits { get; set; } = [];

    /// <summary>
    /// Gets or sets the order of pages, questions and choices drawn for this response.
    /// </summary>
    public SurveyResponsePlan Plan { get; set; } = new();

    /// <summary>
    /// Gets or sets the answers by question id.
    /// </summary>
    public Dictionary<string, SurveyAnswer> Answers { get; set; } = [];

    /// <summary>
    /// Gets or sets the hidden and calculated variables.
    /// </summary>
    public Dictionary<string, string> Variables { get; set; } = [];

    /// <summary>
    /// Gets or sets the score.
    /// </summary>
    public decimal? Score { get; set; }

    /// <summary>
    /// Gets or sets the most points possible.
    /// </summary>
    public decimal? MaxScore { get; set; }

    /// <summary>
    /// Gets or sets the score as a percent.
    /// </summary>
    public decimal? Percent { get; set; }

    /// <summary>
    /// Gets or sets whether the response passed, or null without a pass mark.
    /// </summary>
    public bool? Passed { get; set; }

    /// <summary>
    /// Gets or sets the score of each category by id.
    /// </summary>
    public Dictionary<string, decimal> CategoryScores { get; set; } = [];

    /// <summary>
    /// Gets or sets the id of the score band reached.
    /// </summary>
    public string ScoreBandId { get; set; }

    /// <summary>
    /// Gets or sets the id of the ending used.
    /// </summary>
    public string EndingId { get; set; }

    /// <summary>
    /// Gets or sets the id of the user who graded it.
    /// </summary>
    public string GradedBy { get; set; }

    /// <summary>
    /// Gets or sets when it was graded, in UTC.
    /// </summary>
    public DateTime? GradedUtc { get; set; }

    /// <summary>
    /// Gets or sets the grader's overall feedback.
    /// </summary>
    public string Feedback { get; set; }

    /// <summary>
    /// Gets or sets every change of the score, with who and why.
    /// </summary>
    public List<ScoreChange> ScoreHistory { get; set; } = [];

    /// <summary>
    /// Gets or sets the IP address, only for identified responses when the survey collects it.
    /// </summary>
    public string IpAddress { get; set; }

    /// <summary>
    /// Gets or sets the SHA-256 of the token that lets a respondent who is not signed in resume.
    /// </summary>
    public string ResumeTokenHash { get; set; }

    /// <summary>
    /// Gets or sets, on an anonymous response started from a personal invitation, an HMAC of the invitation id with the
    /// survey's protected secret, so completing the response can mark the invitation completed without the response
    /// naming it. It is cleared when the response completes.
    /// </summary>
    public string InvitationSeal { get; set; }
}
