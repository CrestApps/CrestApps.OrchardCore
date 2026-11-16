using System.Globalization;
using System.Text.RegularExpressions;
using CrestApps.OrchardCore.Learning.Packages.Models;

namespace CrestApps.OrchardCore.Learning.Packages.Core.Scorm;

/// <summary>
/// The SCORM 2004 run-time data model (ADL SCORM 2004 3rd and 4th Edition, Run-Time Environment, section 4), checked on
/// the server: every element's access, vocabulary, data type and range, the <c>_children</c>, <c>_count</c> and
/// <c>_version</c> keywords, array indexes and the order elements must be set in, the uniqueness of objective ids, and
/// the error codes. It holds no state of its own.
/// </summary>
/// <remarks>
/// <para>An element is matched by its pattern, with each array index replaced by <c>n</c>. A read names an existing
/// entry only (301 otherwise). A write may name the next entry (the index equal to the count), which adds it; an
/// objective or an interaction is added only by its <c>id</c>, so any other element of a new entry is a dependency error
/// (408), and so are a response or a correct response pattern before the interaction's <c>type</c>.</para>
/// <para>The data types are the RTE's: <c>characterstring</c> and <c>localized_string_type</c> (with an optional
/// <c>{lang=...}</c> delimiter) up to their smallest permitted maximum, <c>long_identifier_type</c> and
/// <c>short_identifier_type</c>, <c>real(10,7)</c> with the element's range (407 outside it), <c>time(second,10,0)</c>
/// (an ISO 8601 timestamp from 1970 to 2038) and <c>timeinterval(second,10,2)</c> (an ISO 8601 duration). A value
/// longer than the smallest permitted maximum is refused as a type mismatch, so the store stays bounded.</para>
/// <para><c>cmi.completion_status</c> and <c>cmi.success_status</c> are read evaluated (RTE 4.2.4.1 and 4.2.22.1):
/// against <c>cmi.completion_threshold</c> when the package has one, and against <c>cmi.scaled_passing_score</c> when it
/// has one. <c>adl.nav.request</c> is checked and stored; navigation requests are not acted on, and every
/// <c>adl.nav.request_valid</c> element answers <c>unknown</c>. The 4th Edition's <c>adl.data</c> answers a count of 0:
/// shared data stores declared in a manifest are not implemented.</para>
/// <para>The browser script of the package frame (<c>scorm2004-api.js</c>) implements the same table so the content
/// gets its error codes at once.</para>
/// </remarks>
public static partial class Scorm2004DataModel
{
    /// <summary>The data model version, <c>cmi._version</c>.</summary>
    public const string Version = "1.0";

    private const string ScoreChildren = "scaled,raw,min,max";
    private const string CommentChildren = "comment,location,timestamp";

    private static readonly string[] _completionStatuses = ["completed", "incomplete", "not attempted", "unknown"];
    private static readonly string[] _successStatuses = ["passed", "failed", "unknown"];
    private static readonly string[] _exits = ["time-out", "suspend", "logout", "normal", string.Empty];
    private static readonly string[] _interactionTypes = ["true-false", "choice", "fill-in", "long-fill-in", "matching", "performance", "sequencing", "likert", "numeric", "other"];
    private static readonly string[] _singleResponseTypes = ["true-false", "likert", "numeric", "other"];
    private static readonly string[] _results = ["correct", "incorrect", "unanticipated", "neutral"];
    private static readonly string[] _captioning = ["-1", "0", "1"];
    private static readonly string[] _navRequests = ["continue", "previous", "exit", "exitAll", "abandon", "abandonAll", "suspendAll", "_none_"];

    private static readonly Dictionary<string, Element> _elements = Build();
    private static readonly HashSet<string> _known = BuildKnown();

    /// <summary>
    /// Reads an element, as <c>GetValue</c> does once the API is initialized.
    /// </summary>
    /// <param name="cmi">The data.</param>
    /// <param name="element">The element, such as <c>cmi.completion_status</c>.</param>
    /// <returns>The value and the error code.</returns>
    public static Scorm2004Read Get(Scorm2004Cmi cmi, string element)
    {
        ArgumentNullException.ThrowIfNull(cmi);

        if (string.IsNullOrEmpty(element))
        {
            return Fail(Scorm2004Errors.GeneralGetFailure);
        }

        if (element.StartsWith("adl.nav.request_valid.", StringComparison.Ordinal))
        {
            return ReadRequestValid(cmi, element["adl.nav.request_valid.".Length..]);
        }

        var (pattern, indexes) = Parse(element);

        if (pattern is null)
        {
            return Fail(Scorm2004Errors.UndefinedDataModelElement);
        }

        if (!_elements.TryGetValue(pattern, out var definition))
        {
            return Fail(KeywordParentIsKnown(pattern) ? Scorm2004Errors.GeneralGetFailure : Scorm2004Errors.UndefinedDataModelElement);
        }

        if (definition.Access == Access.WriteOnly)
        {
            return Fail(Scorm2004Errors.WriteOnly);
        }

        if (!IndexesExist(cmi, pattern, indexes))
        {
            return Fail(Scorm2004Errors.GeneralGetFailure);
        }

        var value = definition.Get(cmi, indexes);

        return value is null ? Fail(Scorm2004Errors.ValueNotInitialized) : new Scorm2004Read(value, Scorm2004Errors.NoError);
    }

    /// <summary>
    /// Writes an element, as <c>SetValue</c> does once the API is initialized.
    /// </summary>
    /// <param name="cmi">The data.</param>
    /// <param name="element">The element.</param>
    /// <param name="value">The value.</param>
    /// <returns>The error code; <see cref="Scorm2004Errors.NoError"/> when the value was written.</returns>
    public static string Set(Scorm2004Cmi cmi, string element, string value)
    {
        ArgumentNullException.ThrowIfNull(cmi);

        if (string.IsNullOrEmpty(element))
        {
            return Scorm2004Errors.GeneralSetFailure;
        }

        if (element.StartsWith("adl.nav.request_valid.", StringComparison.Ordinal))
        {
            return IsRequestValidElement(cmi, element["adl.nav.request_valid.".Length..]) ? Scorm2004Errors.ReadOnly : Scorm2004Errors.UndefinedDataModelElement;
        }

        var (pattern, indexes) = Parse(element);

        if (pattern is null)
        {
            return Scorm2004Errors.UndefinedDataModelElement;
        }

        if (!_elements.TryGetValue(pattern, out var definition))
        {
            return KeywordParentIsKnown(pattern) ? Scorm2004Errors.ReadOnly : Scorm2004Errors.UndefinedDataModelElement;
        }

        if (definition.Access is Access.Keyword or Access.ReadOnly)
        {
            return Scorm2004Errors.ReadOnly;
        }

        var indexError = CheckWriteIndexes(cmi, pattern, indexes);

        if (indexError != Scorm2004Errors.NoError)
        {
            return indexError;
        }

        if (value is null)
        {
            return Scorm2004Errors.TypeMismatch;
        }

        var error = definition.Check(cmi, indexes, value);

        if (error != Scorm2004Errors.NoError)
        {
            return error;
        }

        definition.Set(cmi, indexes, value);

        return Scorm2004Errors.NoError;
    }

    /// <summary>
    /// Gets <c>cmi.completion_status</c> as a read returns it (RTE 4.2.4.1): with a completion threshold, <c>completed</c>
    /// when the progress measure reaches it, <c>incomplete</c> when it does not, and <c>unknown</c> without a progress
    /// measure; without a threshold, the value the content set.
    /// </summary>
    /// <param name="cmi">The data.</param>
    /// <returns>The status.</returns>
    public static string EvaluateCompletionStatus(Scorm2004Cmi cmi)
    {
        ArgumentNullException.ThrowIfNull(cmi);

        if (!TryParseReal(cmi.CompletionThreshold, out var threshold))
        {
            return cmi.CompletionStatus ?? "unknown";
        }

        if (!TryParseReal(cmi.ProgressMeasure, out var progress))
        {
            return "unknown";
        }

        return progress >= threshold ? "completed" : "incomplete";
    }

    /// <summary>
    /// Gets <c>cmi.success_status</c> as a read returns it (RTE 4.2.22.1): with a scaled passing score, <c>passed</c>
    /// when the scaled score reaches it, <c>failed</c> when it does not, and <c>unknown</c> without a scaled score;
    /// without a passing score, the value the content set.
    /// </summary>
    /// <param name="cmi">The data.</param>
    /// <returns>The status.</returns>
    public static string EvaluateSuccessStatus(Scorm2004Cmi cmi)
    {
        ArgumentNullException.ThrowIfNull(cmi);

        if (!TryParseReal(cmi.ScaledPassingScore, out var passing))
        {
            return cmi.SuccessStatus ?? "unknown";
        }

        if (!TryParseReal(cmi.ScoreScaled, out var scaled))
        {
            return "unknown";
        }

        return scaled >= passing ? "passed" : "failed";
    }

    /// <summary>
    /// Reads a <c>real(10,7)</c> value.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="number">The number.</param>
    /// <returns><see langword="true"/> when it is a real number.</returns>
    public static bool TryParseReal(string value, out decimal number)
    {
        number = 0;

        return !string.IsNullOrEmpty(value) && RealPattern().IsMatch(value) &&
            decimal.TryParse(value, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out number);
    }

    // adl.nav.request_valid.continue and .previous, and .choice.{target=ID} and (4th Edition) .jump.{target=ID}: without
    // sequencing the LMS cannot tell, so every one answers "unknown".
    private static Scorm2004Read ReadRequestValid(Scorm2004Cmi cmi, string rest)
        => IsRequestValidElement(cmi, rest)
            ? new Scorm2004Read("unknown", Scorm2004Errors.NoError)
            : Fail(rest.StartsWith("choice", StringComparison.Ordinal) || rest.StartsWith("jump", StringComparison.Ordinal) ? Scorm2004Errors.GeneralGetFailure : Scorm2004Errors.UndefinedDataModelElement);

    private static bool IsRequestValidElement(Scorm2004Cmi cmi, string rest)
    {
        if (rest is "continue" or "previous")
        {
            return true;
        }

        var match = RequestValidTargetPattern().Match(rest);

        return match.Success && (match.Groups["request"].Value == "choice" || cmi.Edition >= 4);
    }

    // A _children, _count or _version of an element the model knows, which is not one of its keywords: a read is a
    // general get failure (301) and a write a read-only error (404); anything else is an undefined element (401).
    private static bool KeywordParentIsKnown(string pattern)
    {
        foreach (var keyword in new[] { "._children", "._count", "._version" })
        {
            if (pattern.EndsWith(keyword, StringComparison.Ordinal))
            {
                return _known.Contains(pattern[..^keyword.Length]);
            }
        }

        return false;
    }

    private static (string Pattern, int[] Indexes) Parse(string element)
    {
        if (element.Length > 1000)
        {
            return (null, null);
        }

        var segments = element.Split('.');
        var indexes = new List<int>();

        for (var i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];

            if (segment.Length == 0)
            {
                return (null, null);
            }

            if (segment.All(char.IsAsciiDigit))
            {
                if (segment.Length > 5 || !int.TryParse(segment, NumberStyles.None, CultureInfo.InvariantCulture, out var index))
                {
                    return (null, null);
                }

                indexes.Add(index);
                segments[i] = "n";
            }
        }

        return (string.Join('.', segments), indexes.ToArray());
    }

    // A read names existing entries only, at every level.
    private static bool IndexesExist(Scorm2004Cmi cmi, string pattern, int[] indexes)
    {
        if (indexes.Length == 0)
        {
            return true;
        }

        var count = TopCount(cmi, pattern);

        if (count is null || indexes[0] >= count)
        {
            return false;
        }

        if (indexes.Length == 1)
        {
            return true;
        }

        return indexes[1] < SubList(cmi.Interactions[indexes[0]], pattern).Count;
    }

    // A write may add the next entry. An objective or an interaction is added by its id only; a nested entry needs its
    // interaction; a correct response pattern needs the interaction's type.
    private static string CheckWriteIndexes(Scorm2004Cmi cmi, string pattern, int[] indexes)
    {
        if (indexes.Length == 0)
        {
            return Scorm2004Errors.NoError;
        }

        var count = TopCount(cmi, pattern);

        if (count is null || indexes[0] > count)
        {
            return Scorm2004Errors.GeneralSetFailure;
        }

        if (indexes[0] == count)
        {
            if (pattern.StartsWith("adl.data.", StringComparison.Ordinal))
            {
                return Scorm2004Errors.GeneralSetFailure;
            }

            var addsById = pattern is "cmi.objectives.n.id" or "cmi.interactions.n.id";

            if (!addsById && !pattern.StartsWith("cmi.comments_from_learner.", StringComparison.Ordinal))
            {
                return Scorm2004Errors.DependencyNotEstablished;
            }
        }

        if (indexes.Length == 1)
        {
            return Scorm2004Errors.NoError;
        }

        var interaction = cmi.Interactions[indexes[0]];

        if (indexes[1] > SubList(interaction, pattern).Count)
        {
            return Scorm2004Errors.GeneralSetFailure;
        }

        return Scorm2004Errors.NoError;
    }

    private static int? TopCount(Scorm2004Cmi cmi, string pattern)
    {
        if (pattern.StartsWith("cmi.objectives.n.", StringComparison.Ordinal))
        {
            return cmi.Objectives.Count;
        }

        if (pattern.StartsWith("cmi.interactions.n.", StringComparison.Ordinal))
        {
            return cmi.Interactions.Count;
        }

        if (pattern.StartsWith("cmi.comments_from_learner.n.", StringComparison.Ordinal))
        {
            return cmi.CommentsFromLearner.Count;
        }

        if (pattern.StartsWith("cmi.comments_from_lms.n.", StringComparison.Ordinal))
        {
            return cmi.CommentsFromLms.Count;
        }

        if (pattern.StartsWith("adl.data.n.", StringComparison.Ordinal))
        {
            return 0;
        }

        return null;
    }

    private static List<string> SubList(Scorm2004Interaction interaction, string pattern)
        => pattern.StartsWith("cmi.interactions.n.objectives.", StringComparison.Ordinal) ? interaction.ObjectiveIds : interaction.CorrectResponses;

    private static Scorm2004Read Fail(string error)
        => new(string.Empty, error);

    private static T At<T>(List<T> list, int index)
        where T : new()
    {
        if (index == list.Count)
        {
            list.Add(new T());
        }

        return list[index];
    }

    private static void SetAt(List<string> list, int index, string value)
    {
        if (index == list.Count)
        {
            list.Add(value);
        }
        else
        {
            list[index] = value;
        }
    }

    private static Dictionary<string, Element> Build()
    {
        var elements = new Dictionary<string, Element>(StringComparer.Ordinal);

        void Keyword(string pattern, Func<Scorm2004Cmi, int[], string> get)
            => elements[pattern] = new Element(Access.Keyword, get, null, null);

        void ReadOnly(string pattern, Func<Scorm2004Cmi, int[], string> get)
            => elements[pattern] = new Element(Access.ReadOnly, get, null, null);

        void ReadWrite(string pattern, Func<Scorm2004Cmi, int[], string> get, Action<Scorm2004Cmi, int[], string> set, Func<Scorm2004Cmi, int[], string, string> check)
            => elements[pattern] = new Element(Access.ReadWrite, get, set, check);

        void WriteOnly(string pattern, Action<Scorm2004Cmi, int[], string> set, Func<Scorm2004Cmi, int[], string, string> check)
            => elements[pattern] = new Element(Access.WriteOnly, null, set, check);

        Keyword("cmi._version", (_, _) => Version);

        Keyword("cmi.comments_from_learner._children", (_, _) => CommentChildren);
        Keyword("cmi.comments_from_learner._count", (cmi, _) => Count(cmi.CommentsFromLearner.Count));
        ReadWrite("cmi.comments_from_learner.n.comment", (cmi, i) => cmi.CommentsFromLearner[i[0]].Comment, (cmi, i, v) => At(cmi.CommentsFromLearner, i[0]).Comment = v, Check(v => LocalizedString(v, 4000)));
        ReadWrite("cmi.comments_from_learner.n.location", (cmi, i) => cmi.CommentsFromLearner[i[0]].Location, (cmi, i, v) => At(cmi.CommentsFromLearner, i[0]).Location = v, Check(v => CharacterString(v, 250)));
        ReadWrite("cmi.comments_from_learner.n.timestamp", (cmi, i) => cmi.CommentsFromLearner[i[0]].Timestamp, (cmi, i, v) => At(cmi.CommentsFromLearner, i[0]).Timestamp = v, Check(Timestamp));

        Keyword("cmi.comments_from_lms._children", (_, _) => CommentChildren);
        Keyword("cmi.comments_from_lms._count", (cmi, _) => Count(cmi.CommentsFromLms.Count));
        ReadOnly("cmi.comments_from_lms.n.comment", (cmi, i) => cmi.CommentsFromLms[i[0]].Comment);
        ReadOnly("cmi.comments_from_lms.n.location", (cmi, i) => cmi.CommentsFromLms[i[0]].Location);
        ReadOnly("cmi.comments_from_lms.n.timestamp", (cmi, i) => cmi.CommentsFromLms[i[0]].Timestamp);

        ReadWrite("cmi.completion_status", (cmi, _) => EvaluateCompletionStatus(cmi), (cmi, _, v) => cmi.CompletionStatus = v, Check(v => Vocabulary(v, _completionStatuses)));
        ReadOnly("cmi.completion_threshold", (cmi, _) => cmi.CompletionThreshold);
        ReadOnly("cmi.credit", (cmi, _) => cmi.Credit);
        ReadOnly("cmi.entry", (cmi, _) => cmi.Entry);
        WriteOnly("cmi.exit", (cmi, _, v) => cmi.Exit = v, Check(v => Vocabulary(v, _exits)));

        Keyword("cmi.interactions._children", (_, _) => "id,type,objectives,timestamp,correct_responses,weighting,learner_response,result,latency,description");
        Keyword("cmi.interactions._count", (cmi, _) => Count(cmi.Interactions.Count));
        ReadWrite("cmi.interactions.n.id", (cmi, i) => cmi.Interactions[i[0]].Id, (cmi, i, v) => At(cmi.Interactions, i[0]).Id = v, Check(v => Identifier(v, 4000)));
        ReadWrite("cmi.interactions.n.type", (cmi, i) => cmi.Interactions[i[0]].Type, (cmi, i, v) => cmi.Interactions[i[0]].Type = v, Check(v => Vocabulary(v, _interactionTypes)));
        Keyword("cmi.interactions.n.objectives._count", (cmi, i) => Count(cmi.Interactions[i[0]].ObjectiveIds.Count));
        ReadWrite("cmi.interactions.n.objectives.n.id", (cmi, i) => cmi.Interactions[i[0]].ObjectiveIds[i[1]], (cmi, i, v) => SetAt(cmi.Interactions[i[0]].ObjectiveIds, i[1], v), CheckInteractionObjectiveId);
        ReadWrite("cmi.interactions.n.timestamp", (cmi, i) => cmi.Interactions[i[0]].Timestamp, (cmi, i, v) => cmi.Interactions[i[0]].Timestamp = v, Check(Timestamp));
        Keyword("cmi.interactions.n.correct_responses._count", (cmi, i) => Count(cmi.Interactions[i[0]].CorrectResponses.Count));
        ReadWrite("cmi.interactions.n.correct_responses.n.pattern", (cmi, i) => cmi.Interactions[i[0]].CorrectResponses[i[1]], (cmi, i, v) => SetAt(cmi.Interactions[i[0]].CorrectResponses, i[1], v), CheckCorrectResponse);
        ReadWrite("cmi.interactions.n.weighting", (cmi, i) => cmi.Interactions[i[0]].Weighting, (cmi, i, v) => cmi.Interactions[i[0]].Weighting = v, Check(v => Real(v, null, null)));
        ReadWrite("cmi.interactions.n.learner_response", (cmi, i) => cmi.Interactions[i[0]].LearnerResponse, (cmi, i, v) => cmi.Interactions[i[0]].LearnerResponse = v, CheckLearnerResponse);
        ReadWrite("cmi.interactions.n.result", (cmi, i) => cmi.Interactions[i[0]].Result, (cmi, i, v) => cmi.Interactions[i[0]].Result = v, Check(v => _results.Contains(v, StringComparer.Ordinal) ? Scorm2004Errors.NoError : Real(v, null, null)));
        ReadWrite("cmi.interactions.n.latency", (cmi, i) => cmi.Interactions[i[0]].Latency, (cmi, i, v) => cmi.Interactions[i[0]].Latency = v, Check(Duration));
        ReadWrite("cmi.interactions.n.description", (cmi, i) => cmi.Interactions[i[0]].Description, (cmi, i, v) => cmi.Interactions[i[0]].Description = v, Check(v => LocalizedString(v, 250)));

        ReadOnly("cmi.launch_data", (cmi, _) => cmi.LaunchData);
        ReadOnly("cmi.learner_id", (cmi, _) => cmi.LearnerId);
        ReadOnly("cmi.learner_name", (cmi, _) => cmi.LearnerName);

        Keyword("cmi.learner_preference._children", (_, _) => "audio_level,language,delivery_speed,audio_captioning");
        ReadWrite("cmi.learner_preference.audio_level", (cmi, _) => cmi.AudioLevel, (cmi, _, v) => cmi.AudioLevel = v, Check(v => Real(v, 0, null)));
        ReadWrite("cmi.learner_preference.language", (cmi, _) => cmi.Language, (cmi, _, v) => cmi.Language = v, Check(v => v.Length == 0 || IsLanguage(v) ? Scorm2004Errors.NoError : Scorm2004Errors.TypeMismatch));
        ReadWrite("cmi.learner_preference.delivery_speed", (cmi, _) => cmi.DeliverySpeed, (cmi, _, v) => cmi.DeliverySpeed = v, Check(v => Real(v, 0, null)));
        ReadWrite("cmi.learner_preference.audio_captioning", (cmi, _) => cmi.AudioCaptioning, (cmi, _, v) => cmi.AudioCaptioning = v, Check(v => Vocabulary(v, _captioning)));

        ReadWrite("cmi.location", (cmi, _) => cmi.Location, (cmi, _, v) => cmi.Location = v, Check(v => CharacterString(v, 1000)));
        ReadOnly("cmi.max_time_allowed", (cmi, _) => cmi.MaxTimeAllowed);
        ReadOnly("cmi.mode", (cmi, _) => cmi.Mode);

        Keyword("cmi.objectives._children", (_, _) => "id,score,success_status,completion_status,progress_measure,description");
        Keyword("cmi.objectives._count", (cmi, _) => Count(cmi.Objectives.Count));
        ReadWrite("cmi.objectives.n.id", (cmi, i) => cmi.Objectives[i[0]].Id, (cmi, i, v) => At(cmi.Objectives, i[0]).Id = v, CheckObjectiveId);
        Keyword("cmi.objectives.n.score._children", (_, _) => ScoreChildren);
        ReadWrite("cmi.objectives.n.score.scaled", (cmi, i) => cmi.Objectives[i[0]].ScoreScaled, (cmi, i, v) => cmi.Objectives[i[0]].ScoreScaled = v, Check(v => Real(v, -1, 1)));
        ReadWrite("cmi.objectives.n.score.raw", (cmi, i) => cmi.Objectives[i[0]].ScoreRaw, (cmi, i, v) => cmi.Objectives[i[0]].ScoreRaw = v, Check(v => Real(v, null, null)));
        ReadWrite("cmi.objectives.n.score.min", (cmi, i) => cmi.Objectives[i[0]].ScoreMin, (cmi, i, v) => cmi.Objectives[i[0]].ScoreMin = v, Check(v => Real(v, null, null)));
        ReadWrite("cmi.objectives.n.score.max", (cmi, i) => cmi.Objectives[i[0]].ScoreMax, (cmi, i, v) => cmi.Objectives[i[0]].ScoreMax = v, Check(v => Real(v, null, null)));
        ReadWrite("cmi.objectives.n.success_status", (cmi, i) => cmi.Objectives[i[0]].SuccessStatus, (cmi, i, v) => cmi.Objectives[i[0]].SuccessStatus = v, Check(v => Vocabulary(v, _successStatuses)));
        ReadWrite("cmi.objectives.n.completion_status", (cmi, i) => cmi.Objectives[i[0]].CompletionStatus, (cmi, i, v) => cmi.Objectives[i[0]].CompletionStatus = v, Check(v => Vocabulary(v, _completionStatuses)));
        ReadWrite("cmi.objectives.n.progress_measure", (cmi, i) => cmi.Objectives[i[0]].ProgressMeasure, (cmi, i, v) => cmi.Objectives[i[0]].ProgressMeasure = v, Check(v => Real(v, 0, 1)));
        ReadWrite("cmi.objectives.n.description", (cmi, i) => cmi.Objectives[i[0]].Description, (cmi, i, v) => cmi.Objectives[i[0]].Description = v, Check(v => LocalizedString(v, 250)));

        ReadWrite("cmi.progress_measure", (cmi, _) => cmi.ProgressMeasure, (cmi, _, v) => cmi.ProgressMeasure = v, Check(v => Real(v, 0, 1)));
        ReadOnly("cmi.scaled_passing_score", (cmi, _) => cmi.ScaledPassingScore);

        Keyword("cmi.score._children", (_, _) => ScoreChildren);
        ReadWrite("cmi.score.scaled", (cmi, _) => cmi.ScoreScaled, (cmi, _, v) => cmi.ScoreScaled = v, Check(v => Real(v, -1, 1)));
        ReadWrite("cmi.score.raw", (cmi, _) => cmi.ScoreRaw, (cmi, _, v) => cmi.ScoreRaw = v, Check(v => Real(v, null, null)));
        ReadWrite("cmi.score.min", (cmi, _) => cmi.ScoreMin, (cmi, _, v) => cmi.ScoreMin = v, Check(v => Real(v, null, null)));
        ReadWrite("cmi.score.max", (cmi, _) => cmi.ScoreMax, (cmi, _, v) => cmi.ScoreMax = v, Check(v => Real(v, null, null)));

        WriteOnly("cmi.session_time", (cmi, _, v) => cmi.SessionTime = v, Check(Duration));
        ReadWrite("cmi.success_status", (cmi, _) => EvaluateSuccessStatus(cmi), (cmi, _, v) => cmi.SuccessStatus = v, Check(v => Vocabulary(v, _successStatuses)));
        ReadWrite("cmi.suspend_data", (cmi, _) => cmi.SuspendData, (cmi, _, v) => cmi.SuspendData = v, (cmi, _, v) => CharacterString(v, SuspendDataLength(cmi)));
        ReadOnly("cmi.time_limit_action", (cmi, _) => cmi.TimeLimitAction);
        ReadOnly("cmi.total_time", (cmi, _) => cmi.TotalTime);

        ReadWrite("adl.nav.request", (cmi, _) => cmi.NavRequest, (cmi, _, v) => cmi.NavRequest = v, CheckNavRequest);

        // The 4th Edition's shared data stores (adl.data) come from adlcp:data in the manifest, which is not implemented:
        // a package has none, so the count is 0 and every entry is out of range.
        Keyword("adl.data._children", (_, _) => "id,store");
        Keyword("adl.data._count", (_, _) => "0");
        ReadOnly("adl.data.n.id", (_, _) => null);
        ReadWrite("adl.data.n.store", (_, _) => null, (_, _, _) => { }, Check(v => CharacterString(v, 64000)));

        return elements;
    }

    /// <summary>
    /// Gets the most characters <c>cmi.suspend_data</c> takes: 64,000 in the 4th Edition, 4,000 in the 3rd.
    /// </summary>
    /// <param name="cmi">The data.</param>
    /// <returns>The length.</returns>
    public static int SuspendDataLength(Scorm2004Cmi cmi)
        => cmi?.Edition >= 4 ? 64000 : 4000;

    // Every element and every category above one: the parents a _children or _count may be asked of.
    private static HashSet<string> BuildKnown()
    {
        var known = new HashSet<string>(StringComparer.Ordinal);

        foreach (var pattern in _elements.Keys)
        {
            var segments = pattern.Split('.');

            for (var length = 2; length <= segments.Length; length++)
            {
                var prefix = string.Join('.', segments.Take(length));

                if (!prefix.EndsWith("._children", StringComparison.Ordinal) && !prefix.EndsWith("._count", StringComparison.Ordinal) && !prefix.EndsWith("._version", StringComparison.Ordinal))
                {
                    known.Add(prefix);
                }
            }
        }

        return known;
    }

    private static string Count(int count)
        => count.ToString(CultureInfo.InvariantCulture);

    private static Func<Scorm2004Cmi, int[], string, string> Check(Func<string, string> check)
        => (_, _, value) => check(value);

    private static string CheckObjectiveId(Scorm2004Cmi cmi, int[] indexes, string value)
    {
        var error = Identifier(value, 4000);

        if (error != Scorm2004Errors.NoError)
        {
            return error;
        }

        // Ids are unique among the objectives, and an id once set is not changed to another.
        for (var index = 0; index < cmi.Objectives.Count; index++)
        {
            if (index != indexes[0] && string.Equals(cmi.Objectives[index].Id, value, StringComparison.Ordinal))
            {
                return Scorm2004Errors.GeneralSetFailure;
            }
        }

        if (indexes[0] < cmi.Objectives.Count && cmi.Objectives[indexes[0]].Id is { } current && !string.Equals(current, value, StringComparison.Ordinal))
        {
            return Scorm2004Errors.GeneralSetFailure;
        }

        return Scorm2004Errors.NoError;
    }

    private static string CheckInteractionObjectiveId(Scorm2004Cmi cmi, int[] indexes, string value)
    {
        var error = Identifier(value, 4000);

        if (error != Scorm2004Errors.NoError)
        {
            return error;
        }

        var ids = cmi.Interactions[indexes[0]].ObjectiveIds;

        for (var index = 0; index < ids.Count; index++)
        {
            if (index != indexes[1] && string.Equals(ids[index], value, StringComparison.Ordinal))
            {
                return Scorm2004Errors.GeneralSetFailure;
            }
        }

        return Scorm2004Errors.NoError;
    }

    private static string CheckCorrectResponse(Scorm2004Cmi cmi, int[] indexes, string value)
    {
        var type = cmi.Interactions[indexes[0]].Type;

        if (type is null)
        {
            return Scorm2004Errors.DependencyNotEstablished;
        }

        // A true-false, likert, numeric or other interaction has one correct response pattern.
        if (indexes[1] >= 1 && _singleResponseTypes.Contains(type, StringComparer.Ordinal))
        {
            return Scorm2004Errors.GeneralSetFailure;
        }

        return Scorm2004Responses.IsValid(type, value, pattern: true) ? Scorm2004Errors.NoError : Scorm2004Errors.TypeMismatch;
    }

    private static string CheckLearnerResponse(Scorm2004Cmi cmi, int[] indexes, string value)
    {
        var type = cmi.Interactions[indexes[0]].Type;

        if (type is null)
        {
            return Scorm2004Errors.DependencyNotEstablished;
        }

        return Scorm2004Responses.IsValid(type, value, pattern: false) ? Scorm2004Errors.NoError : Scorm2004Errors.TypeMismatch;
    }

    private static string CheckNavRequest(Scorm2004Cmi cmi, int[] indexes, string value)
    {
        if (_navRequests.Contains(value, StringComparer.Ordinal))
        {
            return Scorm2004Errors.NoError;
        }

        var match = NavTargetPattern().Match(value);

        return match.Success && (match.Groups["request"].Value == "choice" || cmi.Edition >= 4) ? Scorm2004Errors.NoError : Scorm2004Errors.TypeMismatch;
    }

    private static string Vocabulary(string value, string[] vocabulary)
        => vocabulary.Contains(value, StringComparer.Ordinal) ? Scorm2004Errors.NoError : Scorm2004Errors.TypeMismatch;

    private static string CharacterString(string value, int maxLength)
        => value.Length <= maxLength ? Scorm2004Errors.NoError : Scorm2004Errors.TypeMismatch;

    private static string LocalizedString(string value, int maxLength)
        => IsLocalizedString(value, maxLength) ? Scorm2004Errors.NoError : Scorm2004Errors.TypeMismatch;

    private static string Identifier(string value, int maxLength)
        => IsIdentifier(value, maxLength) ? Scorm2004Errors.NoError : Scorm2004Errors.TypeMismatch;

    private static string Real(string value, decimal? min, decimal? max)
    {
        if (!TryParseReal(value, out var number))
        {
            return Scorm2004Errors.TypeMismatch;
        }

        return (min is not null && number < min) || (max is not null && number > max) ? Scorm2004Errors.ValueOutOfRange : Scorm2004Errors.NoError;
    }

    private static string Timestamp(string value)
        => IsTimestamp(value) ? Scorm2004Errors.NoError : Scorm2004Errors.TypeMismatch;

    private static string Duration(string value)
        => Scorm2004Rules.IsDuration(value) ? Scorm2004Errors.NoError : Scorm2004Errors.TypeMismatch;

    /// <summary>
    /// Gets whether a value is a <c>localized_string_type</c>: an optional <c>{lang=xx}</c> delimiter with a valid language
    /// tag, then at most the given number of characters.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="maxLength">The most characters after the delimiter.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    internal static bool IsLocalizedString(string value, int maxLength)
    {
        var text = value;

        if (value.StartsWith("{lang=", StringComparison.Ordinal))
        {
            var end = value.IndexOf('}', StringComparison.Ordinal);

            if (end < 0 || !IsLanguage(value[6..end]))
            {
                return false;
            }

            text = value[(end + 1)..];
        }

        return text.Length <= maxLength;
    }

    /// <summary>
    /// Gets whether a value is a <c>language_type</c>: a two or three letter code (or <c>i</c> or <c>x</c>) and subtags of
    /// one to eight letters or digits, at most 250 characters.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    internal static bool IsLanguage(string value)
        => value.Length <= 250 && LanguagePattern().IsMatch(value);

    /// <summary>
    /// Gets whether a value is a <c>long_identifier_type</c> (up to 4,000 characters) or a <c>short_identifier_type</c>
    /// (up to 250): not empty, no white space, and a well-formed URN when it starts with <c>urn:</c>.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="maxLength">The most characters.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    internal static bool IsIdentifier(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length > maxLength || value.Any(char.IsWhiteSpace))
        {
            return false;
        }

        return !value.StartsWith("urn:", StringComparison.OrdinalIgnoreCase) || UrnPattern().IsMatch(value);
    }

    /// <summary>
    /// Gets whether a value is a <c>time(second,10,0)</c>: an ISO 8601 timestamp <c>YYYY[-MM[-DD[Thh[:mm[:ss[.s]]][TZD]]]]</c>
    /// with a year from 1970 to 2038 and every part in its range.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    public static bool IsTimestamp(string value)
    {
        var match = TimestampPattern().Match(value ?? string.Empty);

        if (!match.Success)
        {
            return false;
        }

        static int Part(Match match, string name, int fallback)
            => match.Groups[name].Success ? int.Parse(match.Groups[name].Value, CultureInfo.InvariantCulture) : fallback;

        var year = Part(match, "y", 1970);
        var month = Part(match, "mo", 1);
        var day = Part(match, "d", 1);

        if (year is < 1970 or > 2038 || month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(year, month))
        {
            return false;
        }

        if (Part(match, "h", 0) > 23 || Part(match, "mi", 0) > 59 || Part(match, "s", 0) > 59)
        {
            return false;
        }

        return Part(match, "zh", 0) <= 23 && Part(match, "zm", 0) <= 59;
    }

    [GeneratedRegex("^-?([0-9]+(\\.[0-9]*)?|\\.[0-9]+)$", RegexOptions.CultureInvariant)]
    private static partial Regex RealPattern();

    [GeneratedRegex("^(?:[A-Za-z]{2,3}|[iIxX])(?:-[A-Za-z0-9]{1,8})*$", RegexOptions.CultureInvariant)]
    private static partial Regex LanguagePattern();

    [GeneratedRegex("^urn:[A-Za-z0-9][A-Za-z0-9-]{1,31}:\\S+$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex UrnPattern();

    [GeneratedRegex("^(?<y>[0-9]{4})(?:-(?<mo>[0-9]{2})(?:-(?<d>[0-9]{2})(?:T(?<h>[0-9]{2})(?::(?<mi>[0-9]{2})(?::(?<s>[0-9]{2})(?:\\.[0-9]+)?)?)?(?:Z|[+-](?<zh>[0-9]{2})(?::?(?<zm>[0-9]{2}))?)?)?)?)?$", RegexOptions.CultureInvariant)]
    private static partial Regex TimestampPattern();

    [GeneratedRegex("^\\{target=(?<target>[^}\\s]+)\\}(?<request>choice|jump)$", RegexOptions.CultureInvariant)]
    private static partial Regex NavTargetPattern();

    [GeneratedRegex("^(?<request>choice|jump)\\.\\{target=[^}\\s]+\\}$", RegexOptions.CultureInvariant)]
    private static partial Regex RequestValidTargetPattern();

    private enum Access
    {
        Keyword,
        ReadOnly,
        WriteOnly,
        ReadWrite,
    }

    private sealed record Element(Access Access, Func<Scorm2004Cmi, int[], string> Get, Action<Scorm2004Cmi, int[], string> Set, Func<Scorm2004Cmi, int[], string, string> Check);
}

/// <summary>
/// A value read from the SCORM 2004 data model.
/// </summary>
/// <param name="Value">The value, empty on an error.</param>
/// <param name="Error">The error code.</param>
public readonly record struct Scorm2004Read(string Value, string Error);
