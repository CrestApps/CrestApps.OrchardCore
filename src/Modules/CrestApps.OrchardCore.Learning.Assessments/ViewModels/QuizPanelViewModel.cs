using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Learning.Assessments.ViewModels;

/// <summary>
/// What a learner sees of a quiz: their attempts and whether they may start one.
/// </summary>
public class QuizPanelViewModel
{
    /// <summary>
    /// Gets or sets the id of the quiz course item.
    /// </summary>
    public string QuizId { get; set; }

    /// <summary>
    /// Gets or sets the survey, or <see langword="null"/>.
    /// </summary>
    public Survey Survey { get; set; }

    /// <summary>
    /// Gets or sets the definition of the published version, or <see langword="null"/>.
    /// </summary>
    public SurveyDefinition Definition { get; set; }

    /// <summary>
    /// Gets or sets whether the person is signed in.
    /// </summary>
    public bool SignedIn { get; set; }

    /// <summary>
    /// Gets or sets whether the person may attempt the quiz (enrolled and released).
    /// </summary>
    public bool Enrolled { get; set; }

    /// <summary>
    /// Gets or sets the learner's attempts, newest first.
    /// </summary>
    public IReadOnlyList<SurveyResponse> Attempts { get; set; } = [];

    /// <summary>
    /// Gets or sets the attempt in progress, or <see langword="null"/>.
    /// </summary>
    public SurveyResponse Open { get; set; }

    /// <summary>
    /// Gets or sets whether the attempt in progress is past its deadline, so the button submits it and starts a new one.
    /// </summary>
    public bool OpenExpired { get; set; }

    /// <summary>
    /// Gets or sets how many attempts are left, accommodations included and counted as the quiz's limit counts, or
    /// <see langword="null"/> for no limit.
    /// </summary>
    public int? AttemptsLeft { get; set; }

    /// <summary>
    /// Gets or sets whether the learner may start a new attempt now, as decided by the access rules a start runs.
    /// </summary>
    public bool CanStart { get; set; }

    /// <summary>
    /// Gets or sets whether a new attempt asks for the quiz's access code.
    /// </summary>
    public bool NeedsAccessCode { get; set; }

    /// <summary>
    /// Gets or sets the honor statement the learner accepts before a new attempt, or <see langword="null"/>.
    /// </summary>
    public string Statement { get; set; }

    /// <summary>
    /// Gets or sets what the quiz's review settings let the learner see of each attempt now, by response id. The score
    /// and whether the attempt passed show only when it includes <see cref="ReviewShows.Score"/>.
    /// </summary>
    public IDictionary<string, ReviewShows> Shows { get; set; } = new Dictionary<string, ReviewShows>(StringComparer.Ordinal);

    /// <summary>
    /// Gets what the review settings let the learner see of an attempt now.
    /// </summary>
    /// <param name="attempt">The attempt.</param>
    /// <returns>What shows; nothing when unknown.</returns>
    public ReviewShows ShowsFor(SurveyResponse attempt)
        => attempt?.ItemId is { } id && Shows.TryGetValue(id, out var shows) ? shows : ReviewShows.None;
}
