namespace CrestApps.OrchardCore.Learning.Packages.Models;

/// <summary>
/// The SCORM 2004 run-time data of one learner on one package (ADL SCORM 2004, Run-Time Environment, section 4.2): the
/// <c>cmi</c> data model elements and <c>adl.nav.request</c>. Values are kept as the strings the content set, so a later
/// read returns exactly what was written. A <see langword="null"/> value is an element that was never set, which a read
/// answers with error 403 (value not initialized). The read-only values (the learner, credit, mode, entry, launch data,
/// the thresholds and the time limits) are filled by the server at launch.
/// </summary>
public sealed class Scorm2004Cmi
{
    /// <summary>
    /// Gets or sets the edition of SCORM 2004 the package declares: 3 or 4. It is not a data model element; it sets the
    /// size of <c>cmi.suspend_data</c> (4,000 characters in the 3rd Edition, 64,000 in the 4th) and whether the
    /// <c>jump</c> navigation request exists (4th Edition).
    /// </summary>
    public int Edition { get; set; } = 4;

    /// <summary>Gets or sets <c>cmi.learner_id</c>.</summary>
    public string LearnerId { get; set; } = string.Empty;

    /// <summary>Gets or sets <c>cmi.learner_name</c>.</summary>
    public string LearnerName { get; set; } = string.Empty;

    /// <summary>Gets or sets <c>cmi.location</c>.</summary>
    public string Location { get; set; }

    /// <summary>Gets or sets <c>cmi.suspend_data</c>.</summary>
    public string SuspendData { get; set; }

    /// <summary>Gets or sets <c>cmi.launch_data</c> (<c>adlcp:dataFromLMS</c>).</summary>
    public string LaunchData { get; set; }

    /// <summary>Gets or sets <c>cmi.completion_status</c> as the content set it: <c>completed</c>, <c>incomplete</c>,
    /// <c>not attempted</c> or <c>unknown</c>. A read returns it evaluated against the completion threshold.</summary>
    public string CompletionStatus { get; set; } = "unknown";

    /// <summary>Gets or sets <c>cmi.success_status</c> as the content set it: <c>passed</c>, <c>failed</c> or
    /// <c>unknown</c>. A read returns it evaluated against the scaled passing score.</summary>
    public string SuccessStatus { get; set; } = "unknown";

    /// <summary>Gets or sets <c>cmi.completion_threshold</c>, from 0 to 1.</summary>
    public string CompletionThreshold { get; set; }

    /// <summary>Gets or sets <c>cmi.scaled_passing_score</c>, from -1 to 1.</summary>
    public string ScaledPassingScore { get; set; }

    /// <summary>Gets or sets <c>cmi.progress_measure</c>, from 0 to 1.</summary>
    public string ProgressMeasure { get; set; }

    /// <summary>Gets or sets <c>cmi.score.scaled</c>, from -1 to 1.</summary>
    public string ScoreScaled { get; set; }

    /// <summary>Gets or sets <c>cmi.score.raw</c>.</summary>
    public string ScoreRaw { get; set; }

    /// <summary>Gets or sets <c>cmi.score.min</c>.</summary>
    public string ScoreMin { get; set; }

    /// <summary>Gets or sets <c>cmi.score.max</c>.</summary>
    public string ScoreMax { get; set; }

    /// <summary>Gets or sets <c>cmi.credit</c>: <c>credit</c> or <c>no-credit</c>.</summary>
    public string Credit { get; set; } = "credit";

    /// <summary>Gets or sets <c>cmi.mode</c>: <c>browse</c>, <c>normal</c> or <c>review</c>.</summary>
    public string Mode { get; set; } = "normal";

    /// <summary>Gets or sets <c>cmi.entry</c>: <c>ab-initio</c>, <c>resume</c> or empty.</summary>
    public string Entry { get; set; } = "ab-initio";

    /// <summary>Gets or sets <c>cmi.exit</c> as the content last set it; it decides whether the next launch resumes the
    /// attempt.</summary>
    public string Exit { get; set; } = string.Empty;

    /// <summary>Gets or sets <c>cmi.session_time</c> of the current session, added to the total when it terminates.</summary>
    public string SessionTime { get; set; }

    /// <summary>Gets or sets <c>cmi.total_time</c>, the sum of the session times of the attempt, as an ISO 8601 duration.</summary>
    public string TotalTime { get; set; } = "PT0H0M0S";

    /// <summary>Gets or sets <c>cmi.max_time_allowed</c>, an ISO 8601 duration.</summary>
    public string MaxTimeAllowed { get; set; }

    /// <summary>Gets or sets <c>cmi.time_limit_action</c>.</summary>
    public string TimeLimitAction { get; set; } = "continue,no message";

    /// <summary>Gets or sets <c>cmi.learner_preference.audio_level</c>.</summary>
    public string AudioLevel { get; set; } = "1";

    /// <summary>Gets or sets <c>cmi.learner_preference.language</c>.</summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>Gets or sets <c>cmi.learner_preference.delivery_speed</c>.</summary>
    public string DeliverySpeed { get; set; } = "1";

    /// <summary>Gets or sets <c>cmi.learner_preference.audio_captioning</c>.</summary>
    public string AudioCaptioning { get; set; } = "0";

    /// <summary>Gets or sets <c>adl.nav.request</c> as the content last set it. It is stored and not acted on:
    /// Sequencing and Navigation are not implemented.</summary>
    public string NavRequest { get; set; } = "_none_";

    /// <summary>Gets or sets <c>cmi.objectives</c>.</summary>
    public List<Scorm2004Objective> Objectives { get; set; } = [];

    /// <summary>Gets or sets <c>cmi.interactions</c>.</summary>
    public List<Scorm2004Interaction> Interactions { get; set; } = [];

    /// <summary>Gets or sets <c>cmi.comments_from_learner</c>.</summary>
    public List<Scorm2004Comment> CommentsFromLearner { get; set; } = [];

    /// <summary>Gets or sets <c>cmi.comments_from_lms</c>, which the content only reads.</summary>
    public List<Scorm2004Comment> CommentsFromLms { get; set; } = [];
}

/// <summary>
/// An element of <c>cmi.objectives</c>.
/// </summary>
public sealed class Scorm2004Objective
{
    /// <summary>Gets or sets the id, unique among the objectives; once set it does not change.</summary>
    public string Id { get; set; }

    /// <summary>Gets or sets the scaled score, from -1 to 1.</summary>
    public string ScoreScaled { get; set; }

    /// <summary>Gets or sets the raw score.</summary>
    public string ScoreRaw { get; set; }

    /// <summary>Gets or sets the lowest score.</summary>
    public string ScoreMin { get; set; }

    /// <summary>Gets or sets the highest score.</summary>
    public string ScoreMax { get; set; }

    /// <summary>Gets or sets the success status.</summary>
    public string SuccessStatus { get; set; } = "unknown";

    /// <summary>Gets or sets the completion status.</summary>
    public string CompletionStatus { get; set; } = "unknown";

    /// <summary>Gets or sets the progress measure, from 0 to 1.</summary>
    public string ProgressMeasure { get; set; }

    /// <summary>Gets or sets the description.</summary>
    public string Description { get; set; }
}

/// <summary>
/// An element of <c>cmi.interactions</c>.
/// </summary>
public sealed class Scorm2004Interaction
{
    /// <summary>Gets or sets the id.</summary>
    public string Id { get; set; }

    /// <summary>Gets or sets the type, which decides the format of the responses.</summary>
    public string Type { get; set; }

    /// <summary>Gets or sets the ids of the objectives it counts toward, unique within the interaction.</summary>
    public List<string> ObjectiveIds { get; set; } = [];

    /// <summary>Gets or sets when it happened.</summary>
    public string Timestamp { get; set; }

    /// <summary>Gets or sets the correct response patterns.</summary>
    public List<string> CorrectResponses { get; set; } = [];

    /// <summary>Gets or sets the weighting.</summary>
    public string Weighting { get; set; }

    /// <summary>Gets or sets the learner's response.</summary>
    public string LearnerResponse { get; set; }

    /// <summary>Gets or sets the result.</summary>
    public string Result { get; set; }

    /// <summary>Gets or sets the latency.</summary>
    public string Latency { get; set; }

    /// <summary>Gets or sets the description.</summary>
    public string Description { get; set; }
}

/// <summary>
/// An element of <c>cmi.comments_from_learner</c> or <c>cmi.comments_from_lms</c>.
/// </summary>
public sealed class Scorm2004Comment
{
    /// <summary>Gets or sets the comment.</summary>
    public string Comment { get; set; }

    /// <summary>Gets or sets where in the content it applies.</summary>
    public string Location { get; set; }

    /// <summary>Gets or sets when it was made.</summary>
    public string Timestamp { get; set; }
}
