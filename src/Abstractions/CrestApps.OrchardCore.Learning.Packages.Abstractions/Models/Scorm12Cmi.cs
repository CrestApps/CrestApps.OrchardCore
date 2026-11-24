namespace CrestApps.OrchardCore.Learning.Packages.Models;

/// <summary>
/// The SCORM 1.2 run-time data of one learner on one package: the <c>cmi</c> data model elements. Values are kept as the
/// strings the content set, so a later read returns exactly what was written. The read-only values (the learner, credit,
/// mode, launch data and student data) are filled by the server at launch.
/// </summary>
public sealed class Scorm12Cmi
{
    /// <summary>Gets or sets <c>cmi.core.student_id</c>.</summary>
    public string StudentId { get; set; } = string.Empty;

    /// <summary>Gets or sets <c>cmi.core.student_name</c>.</summary>
    public string StudentName { get; set; } = string.Empty;

    /// <summary>Gets or sets <c>cmi.core.lesson_location</c>.</summary>
    public string LessonLocation { get; set; } = string.Empty;

    /// <summary>Gets or sets <c>cmi.core.credit</c>: <c>credit</c> or <c>no-credit</c>.</summary>
    public string Credit { get; set; } = "credit";

    /// <summary>Gets or sets <c>cmi.core.lesson_status</c>.</summary>
    public string LessonStatus { get; set; } = "not attempted";

    /// <summary>Gets or sets <c>cmi.core.entry</c>: <c>ab-initio</c>, <c>resume</c> or empty.</summary>
    public string Entry { get; set; } = "ab-initio";

    /// <summary>Gets or sets <c>cmi.core.score.raw</c>.</summary>
    public string ScoreRaw { get; set; } = string.Empty;

    /// <summary>Gets or sets <c>cmi.core.score.min</c>.</summary>
    public string ScoreMin { get; set; } = string.Empty;

    /// <summary>Gets or sets <c>cmi.core.score.max</c>.</summary>
    public string ScoreMax { get; set; } = string.Empty;

    /// <summary>Gets or sets <c>cmi.core.total_time</c>, the sum of the session times of finished sessions.</summary>
    public string TotalTime { get; set; } = "0000:00:00.00";

    /// <summary>Gets or sets <c>cmi.core.lesson_mode</c>: <c>normal</c>, <c>review</c> or <c>browse</c>.</summary>
    public string LessonMode { get; set; } = "normal";

    /// <summary>Gets or sets <c>cmi.core.exit</c> as the content last set it; it decides the next entry.</summary>
    public string Exit { get; set; } = string.Empty;

    /// <summary>Gets or sets <c>cmi.core.session_time</c> of the current session, added to the total when it finishes.</summary>
    public string SessionTime { get; set; } = string.Empty;

    /// <summary>Gets or sets <c>cmi.suspend_data</c>.</summary>
    public string SuspendData { get; set; } = string.Empty;

    /// <summary>Gets or sets <c>cmi.launch_data</c>.</summary>
    public string LaunchData { get; set; } = string.Empty;

    /// <summary>Gets or sets <c>cmi.comments</c>.</summary>
    public string Comments { get; set; } = string.Empty;

    /// <summary>Gets or sets <c>cmi.comments_from_lms</c>.</summary>
    public string CommentsFromLms { get; set; } = string.Empty;

    /// <summary>Gets or sets <c>cmi.student_data.mastery_score</c>.</summary>
    public string MasteryScore { get; set; } = string.Empty;

    /// <summary>Gets or sets <c>cmi.student_data.max_time_allowed</c>.</summary>
    public string MaxTimeAllowed { get; set; } = string.Empty;

    /// <summary>Gets or sets <c>cmi.student_data.time_limit_action</c>.</summary>
    public string TimeLimitAction { get; set; } = string.Empty;

    /// <summary>Gets or sets <c>cmi.student_preference.audio</c>.</summary>
    public string Audio { get; set; } = "0";

    /// <summary>Gets or sets <c>cmi.student_preference.language</c>.</summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>Gets or sets <c>cmi.student_preference.speed</c>.</summary>
    public string Speed { get; set; } = "0";

    /// <summary>Gets or sets <c>cmi.student_preference.text</c>.</summary>
    public string Text { get; set; } = "0";

    /// <summary>Gets or sets <c>cmi.objectives</c>.</summary>
    public List<Scorm12Objective> Objectives { get; set; } = [];

    /// <summary>Gets or sets <c>cmi.interactions</c>.</summary>
    public List<Scorm12Interaction> Interactions { get; set; } = [];
}

/// <summary>
/// An element of <c>cmi.objectives</c>.
/// </summary>
public sealed class Scorm12Objective
{
    /// <summary>Gets or sets the id.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the raw score.</summary>
    public string ScoreRaw { get; set; } = string.Empty;

    /// <summary>Gets or sets the lowest score.</summary>
    public string ScoreMin { get; set; } = string.Empty;

    /// <summary>Gets or sets the highest score.</summary>
    public string ScoreMax { get; set; } = string.Empty;

    /// <summary>Gets or sets the status.</summary>
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// An element of <c>cmi.interactions</c>, which the content only writes.
/// </summary>
public sealed class Scorm12Interaction
{
    /// <summary>Gets or sets the id.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the ids of the objectives it counts toward.</summary>
    public List<string> ObjectiveIds { get; set; } = [];

    /// <summary>Gets or sets the time of day it happened.</summary>
    public string Time { get; set; } = string.Empty;

    /// <summary>Gets or sets the type.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Gets or sets the correct response patterns.</summary>
    public List<string> CorrectResponses { get; set; } = [];

    /// <summary>Gets or sets the weighting.</summary>
    public string Weighting { get; set; } = string.Empty;

    /// <summary>Gets or sets the learner's response.</summary>
    public string StudentResponse { get; set; } = string.Empty;

    /// <summary>Gets or sets the result.</summary>
    public string Result { get; set; } = string.Empty;

    /// <summary>Gets or sets the latency.</summary>
    public string Latency { get; set; } = string.Empty;
}
