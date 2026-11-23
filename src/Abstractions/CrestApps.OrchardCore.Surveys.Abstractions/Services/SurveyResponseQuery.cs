using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Which responses to read.
/// </summary>
public sealed class SurveyResponseQuery
{
    /// <summary>
    /// Gets or sets the surveys, or empty for all.
    /// </summary>
    public List<string> SurveyIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the user, or <see langword="null"/>.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the context, <c>Kind:Id</c>, or <see langword="null"/>.
    /// </summary>
    public string ContextRef { get; set; }

    /// <summary>
    /// Gets or sets the context kinds to keep, or empty for any context and responses without one.
    /// </summary>
    public List<string> ContextKinds { get; set; } = [];

    /// <summary>
    /// Gets or sets the context kinds to leave out. Responses without a context are kept.
    /// </summary>
    public List<string> ExcludedContextKinds { get; set; } = [];

    /// <summary>
    /// Gets or sets the statuses, or empty for all.
    /// </summary>
    public List<SurveyResponseStatus> Statuses { get; set; } = [];

    /// <summary>
    /// Gets or sets the statuses left out, such as those of an anonymous survey too few responses share.
    /// </summary>
    public List<SurveyResponseStatus> ExcludedStatuses { get; set; } = [];

    /// <summary>
    /// Gets or sets the surveys left out, such as anonymous surveys below their minimum group size.
    /// </summary>
    public List<string> ExcludedSurveyIds { get; set; } = [];

    /// <summary>
    /// Gets or sets a user whose own responses are left out, such as the grader of a grading queue, or
    /// <see langword="null"/>.
    /// </summary>
    public string ExcludedUserId { get; set; }

    /// <summary>
    /// Gets or sets the grading status, or <see langword="null"/>.
    /// </summary>
    public GradingStatus? Grading { get; set; }

    /// <summary>
    /// Gets or sets the version number, or <see langword="null"/>.
    /// </summary>
    public int? VersionNumber { get; set; }

    /// <summary>
    /// Gets or sets the collector, or <see langword="null"/>.
    /// </summary>
    public string CollectorId { get; set; }

    /// <summary>
    /// Gets or sets whether test responses are included.
    /// </summary>
    public bool IncludeTest { get; set; }

    /// <summary>
    /// Gets or sets the earliest start, in UTC, or <see langword="null"/>.
    /// </summary>
    public DateTime? StartedFromUtc { get; set; }

    /// <summary>
    /// Gets or sets the latest start (exclusive), in UTC, or <see langword="null"/>.
    /// </summary>
    public DateTime? StartedToUtc { get; set; }

    /// <summary>
    /// Gets or sets a search on the response number.
    /// </summary>
    public string Search { get; set; }

    /// <summary>
    /// Gets or sets whether the page is read oldest first, by completion and then start, instead of newest first by
    /// start. Work queues use it so the oldest waiting response comes first.
    /// </summary>
    public bool OldestFirst { get; set; }

    /// <summary>
    /// Gets or sets whether the page is read in the order of the responses' random ids, which orders nothing: lists of
    /// an anonymous survey use it, since start and completion times (and response numbers) would give away the order
    /// people answered in. It takes precedence over <see cref="OldestFirst"/>.
    /// </summary>
    public bool OrderById { get; set; }

    /// <summary>
    /// Gets or sets how many to skip.
    /// </summary>
    public int Skip { get; set; }

    /// <summary>
    /// Gets or sets how many to read, at most <see cref="SurveysConstants.PageSize"/>.
    /// </summary>
    public int Take { get; set; } = 50;
}
