using System.Globalization;
using System.Text.Json;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Core.Moodle;
using CrestApps.OrchardCore.Learning.Models;

namespace CrestApps.OrchardCore.Learning.Assessments.Core.Moodle;

/// <summary>
/// Maps the settings of a Moodle quiz the site keeps: questions per page, the question behaviour and the review options
/// per moment.
/// </summary>
public static class MoodleQuizSettings
{
    // The moments of Moodle's review options, as bits of each review field (mod_quiz display_options).
    private const int During = 0x10000;
    private const int ImmediatelyAfter = 0x01000;
    private const int LaterWhileOpen = 0x00100;
    private const int AfterClose = 0x00010;

    /// <summary>
    /// Gets the questions per page, 0 for every question on one page.
    /// </summary>
    /// <param name="quiz">The quiz.</param>
    /// <returns>The number.</returns>
    public static int QuestionsPerPage(MoodleActivity quiz)
    {
        ArgumentNullException.ThrowIfNull(quiz);

        return int.TryParse(quiz.Get("questionsperpage"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var perPage) && perPage > 0 ? perPage : 0;
    }

    /// <summary>
    /// Gets when the learner gets feedback on an answer: immediately for Moodle's immediate feedback and interactive
    /// behaviours (with or without certainty-based marking), after the attempt otherwise.
    /// </summary>
    /// <param name="quiz">The quiz.</param>
    /// <returns>The feedback mode.</returns>
    public static QuizFeedbackMode FeedbackMode(MoodleActivity quiz)
    {
        ArgumentNullException.ThrowIfNull(quiz);

        return quiz.Get("preferredbehaviour") is "immediatefeedback" or "immediatecbm" or "interactive" or "interactivecountback"
            ? QuizFeedbackMode.Immediate
            : QuizFeedbackMode.Deferred;
    }

    /// <summary>
    /// Gets what the learner sees of an attempt at each moment of the review: the marks, the right answers, the feedback
    /// on each answer and the general feedback, at a moment the attempt itself may be reviewed.
    /// </summary>
    /// <param name="quiz">The quiz.</param>
    /// <returns>The review options.</returns>
    public static QuizReviewOptions ReviewOptions(MoodleActivity quiz)
    {
        ArgumentNullException.ThrowIfNull(quiz);

        return new QuizReviewOptions
        {
            During = Shows(quiz, During),
            ImmediatelyAfter = Shows(quiz, ImmediatelyAfter),
            LaterWhileOpen = Shows(quiz, LaterWhileOpen),
            AfterClose = Shows(quiz, AfterClose),
        };
    }

    /// <summary>
    /// Tells whether a quiz has review options at all; an older backup without them keeps the site's review policy.
    /// </summary>
    /// <param name="quiz">The quiz.</param>
    /// <returns><see langword="true"/> when the quiz names its review options.</returns>
    public static bool HasReviewOptions(MoodleActivity quiz)
        => quiz?.Get("reviewattempt") is not null;

    private static ReviewShows Shows(MoodleActivity quiz, int moment)
    {
        if ((Bits(quiz, "reviewattempt") & moment) == 0)
        {
            return ReviewShows.None;
        }

        var shows = ReviewShows.None;

        if ((Bits(quiz, "reviewmarks") & moment) != 0)
        {
            shows |= ReviewShows.Score;
        }

        if ((Bits(quiz, "reviewrightanswer") & moment) != 0)
        {
            shows |= ReviewShows.RightAnswers;
        }

        if ((Bits(quiz, "reviewspecificfeedback") & moment) != 0)
        {
            shows |= ReviewShows.Feedback;
        }

        if ((Bits(quiz, "reviewgeneralfeedback") & moment) != 0)
        {
            shows |= ReviewShows.GeneralFeedback;
        }

        return shows;
    }

    private static int Bits(MoodleActivity quiz, string name)
        => int.TryParse(quiz.Get(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var bits) ? bits : 0;
}

/// <summary>
/// Maps the settings of a Moodle SCORM activity the site keeps: a window of its own, the attempts and which attempt
/// grades.
/// </summary>
public static class MoodleScormSettings
{
    /// <summary>
    /// Gets whether the package opens in a new window.
    /// </summary>
    /// <param name="scorm">The activity.</param>
    /// <returns><see langword="true"/> for a new window.</returns>
    public static bool OpensInNewWindow(MoodleActivity scorm)
    {
        ArgumentNullException.ThrowIfNull(scorm);

        return scorm.Get("popup") == "1";
    }

    /// <summary>
    /// Gets the number of attempts, or <see langword="null"/> for no limit.
    /// </summary>
    /// <param name="scorm">The activity.</param>
    /// <returns>The limit.</returns>
    public static int? MaxAttempts(MoodleActivity scorm)
    {
        ArgumentNullException.ThrowIfNull(scorm);

        return int.TryParse(scorm.Get("maxattempt"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var attempts) && attempts > 0 ? attempts : null;
    }

    /// <summary>
    /// Gets which attempts set the score (Moodle's <c>whatgrade</c>: highest, average, first, last).
    /// </summary>
    /// <param name="scorm">The activity.</param>
    /// <returns>The grading method.</returns>
    public static GradingMethod Grading(MoodleActivity scorm)
    {
        ArgumentNullException.ThrowIfNull(scorm);

        return scorm.Get("whatgrade") switch
        {
            "1" => GradingMethod.Average,
            "2" => GradingMethod.First,
            "3" => GradingMethod.Latest,
            _ => GradingMethod.Highest,
        };
    }
}

/// <summary>
/// The restrictions of a Moodle activity the site keeps: the people it is open to, a date it opens on, and the
/// conditions it has no equivalent for.
/// </summary>
public sealed class MoodleRestriction
{
    /// <summary>Gets the people, as the profile field (<c>email</c>, <c>username</c>) and its value.</summary>
    public IList<(string Field, string Value)> People { get; } = [];

    /// <summary>Gets or sets the date the activity opens on, in UTC.</summary>
    public DateTime? OpensUtc { get; set; }

    /// <summary>Gets the conditions with no equivalent, such as <c>group</c> or <c>profile:department</c>.</summary>
    public IList<string> Unsupported { get; } = [];

    /// <summary>Gets whether the activity is restricted at all.</summary>
    public bool Any => People.Count > 0 || OpensUtc is not null || Unsupported.Count > 0;
}

/// <summary>
/// Reads Moodle's availability JSON: a person named by e-mail address or user name (a profile condition "is equal to"),
/// an opening date, and every other condition reported by its type.
/// </summary>
public static class MoodleRestrictions
{
    /// <summary>
    /// Reads the restrictions of an activity.
    /// </summary>
    /// <param name="json">The availability JSON, or <see langword="null"/>.</param>
    /// <returns>The restrictions.</returns>
    public static MoodleRestriction Read(string json)
    {
        var restriction = new MoodleRestriction();

        if (string.IsNullOrWhiteSpace(json))
        {
            return restriction;
        }

        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 32 });

            Walk(document.RootElement, restriction);
        }
        catch (JsonException)
        {
            // Availability Moodle did not write is read as none.
        }

        return restriction;
    }

    private static void Walk(JsonElement tree, MoodleRestriction restriction)
    {
        if (tree.ValueKind != JsonValueKind.Object || !tree.TryGetProperty("c", out var conditions) || conditions.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var condition in conditions.EnumerateArray())
        {
            if (condition.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            // A nested tree of conditions.
            if (condition.TryGetProperty("c", out _))
            {
                Walk(condition, restriction);

                continue;
            }

            var type = Text(condition, "type");

            switch (type)
            {
                case "profile" when Text(condition, "sf") is "email" or "username" && Text(condition, "op") == "isequalto" && !string.IsNullOrWhiteSpace(Text(condition, "v")):
                    restriction.People.Add((Text(condition, "sf"), Text(condition, "v").Trim()));
                    break;

                case "profile":
                    restriction.Unsupported.Add("profile:" + (Text(condition, "sf") ?? Text(condition, "cf") ?? "?"));
                    break;

                case "date" when Text(condition, "d") == ">=" && condition.TryGetProperty("t", out var time) && time.TryGetInt64(out var seconds) && seconds > 0:
                    restriction.OpensUtc = DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime;
                    break;

                default:
                    restriction.Unsupported.Add(type ?? "?");
                    break;
            }
        }
    }

    private static string Text(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}

/// <summary>
/// The settings of a Moodle assignment the site keeps on coursework.
/// </summary>
public sealed class MoodleAssignment
{
    /// <summary>Gets or sets the due date, in UTC.</summary>
    public DateTime? DueUtc { get; set; }

    /// <summary>Gets or sets the most points.</summary>
    public decimal MaxPoints { get; set; } = 100;

    /// <summary>Gets or sets whether the assignment is graded on a scale, which is not imported.</summary>
    public bool UsesScale { get; set; }

    /// <summary>Gets or sets whether learners write an online text.</summary>
    public bool AcceptsText { get; set; }

    /// <summary>Gets or sets whether learners hand in files.</summary>
    public bool AcceptsFiles { get; set; }

    /// <summary>Gets or sets the most files, or <see langword="null"/> for the default.</summary>
    public int? MaxFiles { get; set; }

    /// <summary>Gets or sets the largest file, in megabytes, or <see langword="null"/> for the default.</summary>
    public int? MaxFileSizeMb { get; set; }

    /// <summary>Gets or sets whether a learner may hand in again.</summary>
    public bool AllowResubmission { get; set; }

    /// <summary>Gets or sets whether handing in completes the activity.</summary>
    public bool CompletesOnSubmit { get; set; }
}

/// <summary>
/// Reads a Moodle assignment: its due date, its points (a scale is reported), what learners hand in (online text,
/// files, or nothing for a grade-only sign-off), the file limits, resubmission and completion on hand-in.
/// </summary>
public static class MoodleAssignments
{
    /// <summary>
    /// The most files coursework takes by default.
    /// </summary>
    public const int MaxFiles = 20;

    /// <summary>
    /// Reads an assignment.
    /// </summary>
    /// <param name="assign">The activity.</param>
    /// <returns>The settings.</returns>
    public static MoodleAssignment Read(MoodleActivity assign)
    {
        ArgumentNullException.ThrowIfNull(assign);

        var grade = Number(assign.Get("grade"));
        var assignment = new MoodleAssignment
        {
            DueUtc = Number(assign.Get("duedate")) is { } due && due > 0 ? DateTimeOffset.FromUnixTimeSeconds((long)due).UtcDateTime : null,
            MaxPoints = grade is > 0 ? grade.Value : 100,
            UsesScale = grade is < 0,
            AcceptsText = Config(assign, "onlinetext", "enabled") == "1",
            AcceptsFiles = Config(assign, "file", "enabled") == "1",
            AllowResubmission = assign.Get("attemptreopenmethod") is { } reopen && reopen != "none",
            CompletesOnSubmit = assign.Get("completionsubmit") == "1",
        };

        if (assignment.AcceptsFiles && Number(Config(assign, "file", "maxfilesubmissions")) is { } files && files > 0)
        {
            assignment.MaxFiles = (int)Math.Min(files, MaxFiles);
        }

        if (assignment.AcceptsFiles && Number(Config(assign, "file", "maxsubmissionsizebytes")) is { } bytes && bytes > 0)
        {
            assignment.MaxFileSizeMb = (int)Math.Max(1, Math.Ceiling(bytes / (1024m * 1024m)));
        }

        return assignment;
    }

    private static string Config(MoodleActivity assign, string plugin, string name)
        => assign.RecordsOf("plugin_config")
            .Where(config => Value(config, "subtype") == "assignsubmission" && Value(config, "plugin") == plugin && Value(config, "name") == name)
            .Select(config => Value(config, "value"))
            .FirstOrDefault();

    private static string Value(IDictionary<string, string> record, string name)
        => record.TryGetValue(name, out var value) ? value : null;

    private static decimal? Number(string value)
        => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number) ? number : null;
}

/// <summary>
/// The kinds of question a survey of the site has.
/// </summary>
public enum MoodleSurveyQuestionKind
{
    /// <summary>One option.</summary>
    SingleChoice,

    /// <summary>Several options.</summary>
    MultipleChoice,

    /// <summary>A written answer.</summary>
    Text,
}

/// <summary>
/// When a choice shows its totals to learners.
/// </summary>
public enum MoodleSurveyResults
{
    /// <summary>Never.</summary>
    Never,

    /// <summary>After the learner answered.</summary>
    AfterAnswer,

    /// <summary>After the choice closed.</summary>
    AfterClose,

    /// <summary>Always.</summary>
    Always,
}

/// <summary>
/// A question of a Moodle feedback or choice.
/// </summary>
/// <param name="Text">The question.</param>
/// <param name="Kind">The kind.</param>
/// <param name="Options">The options of a choice.</param>
/// <param name="Required">Whether it must be answered.</param>
public sealed record MoodleSurveyQuestion(string Text, MoodleSurveyQuestionKind Kind, IReadOnlyList<string> Options, bool Required);

/// <summary>
/// A Moodle feedback or choice, as a survey of the site.
/// </summary>
public sealed class MoodleSurvey
{
    /// <summary>Gets the questions.</summary>
    public IList<MoodleSurveyQuestion> Questions { get; } = [];

    /// <summary>Gets the kinds of item that were left out, such as <c>captcha</c>.</summary>
    public IList<string> Skipped { get; } = [];

    /// <summary>Gets or sets whether answers are anonymous.</summary>
    public bool Anonymous { get; set; }

    /// <summary>Gets or sets whether a learner may change their answer.</summary>
    public bool AllowChangeAnswer { get; set; }

    /// <summary>Gets or sets when it closes, in UTC.</summary>
    public DateTime? CloseUtc { get; set; }

    /// <summary>Gets or sets when learners see the totals.</summary>
    public MoodleSurveyResults Results { get; set; }
}

/// <summary>
/// Reads Moodle feedbacks and choices as surveys. A feedback's choice items (radio, check boxes, drop-down, rated) become
/// choice questions, its text, text area and number items written answers; labels, information and page breaks show
/// nothing to answer and are left out, and anything else (a captcha) is reported.
/// </summary>
public static class MoodleSurveys
{
    private const string PresentationSeparator = ">>>>>";
    private const string RatedSeparator = "####";

    /// <summary>
    /// Reads a feedback.
    /// </summary>
    /// <param name="feedback">The activity.</param>
    /// <returns>The survey.</returns>
    public static MoodleSurvey FromFeedback(MoodleActivity feedback)
    {
        ArgumentNullException.ThrowIfNull(feedback);

        var survey = new MoodleSurvey
        {
            // Moodle: 1 anonymous, 2 with the learners' names.
            Anonymous = feedback.Get("anonymous") == "1",
            AllowChangeAnswer = feedback.Get("multiple_submit") == "1",
            CloseUtc = Time(feedback.Get("timeclose")),
            Results = MoodleSurveyResults.Never,
        };

        foreach (var item in feedback.RecordsOf("item"))
        {
            var type = Value(item, "typ");
            var text = MoodleQuestionMapper.Plain(Value(item, "name"));
            var required = Value(item, "required") == "1";
            var presentation = Value(item, "presentation") ?? string.Empty;

            switch (type)
            {
                case "multichoice" or "multichoicerated":
                    var separator = presentation.IndexOf(PresentationSeparator, StringComparison.Ordinal);
                    var kind = separator > 0 && presentation[0] == 'c' ? MoodleSurveyQuestionKind.MultipleChoice : MoodleSurveyQuestionKind.SingleChoice;
                    var options = (separator >= 0 ? presentation[(separator + PresentationSeparator.Length)..] : presentation)
                        .Split('|')
                        .Select(option => option.Contains(RatedSeparator, StringComparison.Ordinal) ? option[(option.IndexOf(RatedSeparator, StringComparison.Ordinal) + RatedSeparator.Length)..] : option)
                        .Select(MoodleQuestionMapper.Plain)
                        .Where(option => option.Length > 0)
                        .ToList();

                    if (options.Count > 0)
                    {
                        survey.Questions.Add(new MoodleSurveyQuestion(text, kind, options, required));
                    }

                    break;

                case "textarea" or "textfield" or "numeric":
                    survey.Questions.Add(new MoodleSurveyQuestion(text, MoodleSurveyQuestionKind.Text, [], required));
                    break;

                case "label" or "info" or "pagebreak":
                    break;

                default:
                    survey.Skipped.Add(type ?? "?");
                    break;
            }
        }

        return survey;
    }

    /// <summary>
    /// Reads a choice.
    /// </summary>
    /// <param name="choice">The activity.</param>
    /// <returns>The survey of one question.</returns>
    public static MoodleSurvey FromChoice(MoodleActivity choice)
    {
        ArgumentNullException.ThrowIfNull(choice);

        var question = MoodleQuestionMapper.Plain(choice.Get("intro"));
        var survey = new MoodleSurvey
        {
            // Moodle: publish 0 shows the totals without names.
            Anonymous = choice.Get("publish") != "1",
            AllowChangeAnswer = choice.Get("allowupdate") == "1",
            CloseUtc = Time(choice.Get("timeclose")),
            Results = choice.Get("showresults") switch
            {
                "1" => MoodleSurveyResults.AfterAnswer,
                "2" => MoodleSurveyResults.AfterClose,
                "3" => MoodleSurveyResults.Always,
                _ => MoodleSurveyResults.Never,
            },
        };

        var options = choice.RecordsOf("option").Select(option => MoodleQuestionMapper.Plain(Value(option, "text"))).Where(option => option.Length > 0).ToList();

        survey.Questions.Add(new MoodleSurveyQuestion(
            string.IsNullOrEmpty(question) ? choice.Name : question,
            choice.Get("allowmultiple") == "1" ? MoodleSurveyQuestionKind.MultipleChoice : MoodleSurveyQuestionKind.SingleChoice,
            options,
            true));

        return survey;
    }

    private static string Value(IDictionary<string, string> record, string name)
        => record.TryGetValue(name, out var value) ? value : null;

    private static DateTime? Time(string value)
        => long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds) && seconds > 0
            ? DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime
            : null;
}
