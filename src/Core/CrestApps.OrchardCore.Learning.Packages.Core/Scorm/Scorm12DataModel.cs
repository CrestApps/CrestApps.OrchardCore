using System.Globalization;
using System.Text.RegularExpressions;
using CrestApps.OrchardCore.Learning.Packages.Models;

namespace CrestApps.OrchardCore.Learning.Packages.Core.Scorm;

/// <summary>
/// The SCORM 1.2 run-time data model (ADL SCORM 1.2, Run-Time Environment, section 3.4), checked on the server: every
/// element's access (read-only, write-only, read-write), vocabulary, data type and range, the <c>_children</c>,
/// <c>_count</c> and <c>_version</c> keywords, array indexes, and the error codes. It holds no state of its own.
/// </summary>
/// <remarks>
/// An element is matched by its pattern, with each array index replaced by <c>n</c>. An index may name an existing
/// entry or, on a write, the next one (the index equal to the count), which adds it. Scores are CMIDecimal from 0 to
/// 100 or blank, as LMS implementations of SCORM 1.2 commonly require. The browser script of the package frame
/// (<c>scorm12-api.js</c>) implements the same table so the content gets its error codes at once.
/// </remarks>
public static partial class Scorm12DataModel
{
    private const string CoreChildren = "student_id,student_name,lesson_location,credit,lesson_status,entry,score,total_time,lesson_mode,exit,session_time";
    private const string ScoreChildren = "raw,min,max";

    private static readonly string[] _lessonStatuses = ["passed", "completed", "failed", "incomplete", "browsed"];
    private static readonly string[] _objectiveStatuses = ["passed", "completed", "failed", "incomplete", "browsed", "not attempted"];
    private static readonly string[] _exits = ["time-out", "suspend", "logout", string.Empty];
    private static readonly string[] _interactionTypes = ["true-false", "choice", "fill-in", "matching", "performance", "sequencing", "likert", "numeric"];
    private static readonly string[] _results = ["correct", "wrong", "unanticipated", "neutral"];

    private static readonly Dictionary<string, Element> _elements = Build();
    private static readonly HashSet<string> _known = BuildKnown();

    /// <summary>
    /// Reads an element, as <c>LMSGetValue</c> does once the API is initialized.
    /// </summary>
    /// <param name="cmi">The data.</param>
    /// <param name="element">The element, such as <c>cmi.core.lesson_status</c>.</param>
    /// <returns>The value and the error code.</returns>
    public static Scorm12Read Get(Scorm12Cmi cmi, string element)
    {
        ArgumentNullException.ThrowIfNull(cmi);

        var (pattern, indexes) = Parse(element);

        if (pattern is null)
        {
            return Fail(Scorm12Errors.InvalidArgument);
        }

        if (!_elements.TryGetValue(pattern, out var definition))
        {
            return Fail(KeywordError(pattern, Scorm12Errors.CannotHaveChildren, Scorm12Errors.NotAnArray));
        }

        if (definition.Access == Access.WriteOnly)
        {
            return Fail(Scorm12Errors.WriteOnly);
        }

        // A read names existing entries only.
        if (!IndexesExist(cmi, pattern, indexes, allowNext: false))
        {
            return Fail(Scorm12Errors.InvalidArgument);
        }

        return new Scorm12Read(definition.Get(cmi, indexes) ?? string.Empty, Scorm12Errors.NoError);
    }

    /// <summary>
    /// Writes an element, as <c>LMSSetValue</c> does once the API is initialized. An array element at the index equal to
    /// the count adds a new entry.
    /// </summary>
    /// <param name="cmi">The data.</param>
    /// <param name="element">The element.</param>
    /// <param name="value">The value.</param>
    /// <returns>The error code; <see cref="Scorm12Errors.NoError"/> when the value was written.</returns>
    public static string Set(Scorm12Cmi cmi, string element, string value)
    {
        ArgumentNullException.ThrowIfNull(cmi);

        var (pattern, indexes) = Parse(element);

        if (pattern is null)
        {
            return Scorm12Errors.InvalidArgument;
        }

        if (!_elements.TryGetValue(pattern, out var definition))
        {
            return KeywordError(pattern, Scorm12Errors.InvalidSetKeyword, Scorm12Errors.InvalidSetKeyword);
        }

        if (definition.Access == Access.Keyword)
        {
            return Scorm12Errors.InvalidSetKeyword;
        }

        if (definition.Access == Access.ReadOnly)
        {
            return Scorm12Errors.ReadOnly;
        }

        if (!IndexesExist(cmi, pattern, indexes, allowNext: true))
        {
            return Scorm12Errors.InvalidArgument;
        }

        if (value is null || !definition.IsValid(value))
        {
            return Scorm12Errors.IncorrectDataType;
        }

        definition.Set(cmi, indexes, value);

        return Scorm12Errors.NoError;
    }

    // An element the table does not have: a _children or _count of an element the model knows is a keyword error
    // (202 or 203 on a read, 402 on a write); anything else is an invalid argument.
    private static string KeywordError(string pattern, string childrenError, string countError)
    {
        if (pattern.EndsWith("._children", StringComparison.Ordinal) && _known.Contains(pattern[..^"._children".Length]))
        {
            return childrenError;
        }

        if (pattern.EndsWith("._count", StringComparison.Ordinal) && _known.Contains(pattern[..^"._count".Length]))
        {
            return countError;
        }

        return Scorm12Errors.InvalidArgument;
    }

    private static (string Pattern, int[] Indexes) Parse(string element)
    {
        if (string.IsNullOrEmpty(element) || element.Length > 255)
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

    private static bool IndexesExist(Scorm12Cmi cmi, string pattern, int[] indexes, bool allowNext)
    {
        if (indexes.Length == 0)
        {
            return true;
        }

        if (pattern.StartsWith("cmi.objectives.n.", StringComparison.Ordinal))
        {
            return Within(indexes[0], cmi.Objectives.Count, allowNext);
        }

        if (!pattern.StartsWith("cmi.interactions.n.", StringComparison.Ordinal))
        {
            return false;
        }

        var interactionIndex = indexes[0];

        if (!Within(interactionIndex, cmi.Interactions.Count, allowNext))
        {
            return false;
        }

        if (indexes.Length == 1)
        {
            return true;
        }

        var interaction = interactionIndex < cmi.Interactions.Count ? cmi.Interactions[interactionIndex] : null;
        var count = pattern.StartsWith("cmi.interactions.n.objectives.", StringComparison.Ordinal)
            ? interaction?.ObjectiveIds.Count ?? 0
            : interaction?.CorrectResponses.Count ?? 0;

        return Within(indexes[1], count, allowNext);
    }

    private static bool Within(int index, int count, bool allowNext)
        => index < count || (allowNext && index == count);

    private static Scorm12Read Fail(string error)
        => new(string.Empty, error);

    private static Scorm12Objective Objective(Scorm12Cmi cmi, int index)
    {
        if (index == cmi.Objectives.Count)
        {
            cmi.Objectives.Add(new Scorm12Objective());
        }

        return cmi.Objectives[index];
    }

    private static Scorm12Interaction Interaction(Scorm12Cmi cmi, int index)
    {
        if (index == cmi.Interactions.Count)
        {
            cmi.Interactions.Add(new Scorm12Interaction());
        }

        return cmi.Interactions[index];
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

        void Keyword(string pattern, Func<Scorm12Cmi, int[], string> get)
            => elements[pattern] = new Element(Access.Keyword, get, null, null);

        void ReadOnly(string pattern, Func<Scorm12Cmi, string> get)
            => elements[pattern] = new Element(Access.ReadOnly, (cmi, _) => get(cmi), null, null);

        void ReadWrite(string pattern, Func<Scorm12Cmi, int[], string> get, Action<Scorm12Cmi, int[], string> set, Func<string, bool> valid)
            => elements[pattern] = new Element(Access.ReadWrite, get, set, valid);

        void WriteOnly(string pattern, Action<Scorm12Cmi, int[], string> set, Func<string, bool> valid)
            => elements[pattern] = new Element(Access.WriteOnly, null, set, valid);

        Keyword("cmi._version", (_, _) => "3.4");
        Keyword("cmi._children", (_, _) => "core,suspend_data,launch_data,comments,objectives,student_data,student_preference,interactions");

        Keyword("cmi.core._children", (_, _) => CoreChildren);
        ReadOnly("cmi.core.student_id", cmi => cmi.StudentId);
        ReadOnly("cmi.core.student_name", cmi => cmi.StudentName);
        ReadWrite("cmi.core.lesson_location", (cmi, _) => cmi.LessonLocation, (cmi, _, value) => cmi.LessonLocation = value, value => IsString(value, 255));
        ReadOnly("cmi.core.credit", cmi => cmi.Credit);
        ReadWrite("cmi.core.lesson_status", (cmi, _) => cmi.LessonStatus, (cmi, _, value) => cmi.LessonStatus = value, value => _lessonStatuses.Contains(value, StringComparer.Ordinal));
        ReadOnly("cmi.core.entry", cmi => cmi.Entry);
        Keyword("cmi.core.score._children", (_, _) => ScoreChildren);
        ReadWrite("cmi.core.score.raw", (cmi, _) => cmi.ScoreRaw, (cmi, _, value) => cmi.ScoreRaw = value, IsScore);
        ReadWrite("cmi.core.score.min", (cmi, _) => cmi.ScoreMin, (cmi, _, value) => cmi.ScoreMin = value, IsScore);
        ReadWrite("cmi.core.score.max", (cmi, _) => cmi.ScoreMax, (cmi, _, value) => cmi.ScoreMax = value, IsScore);
        ReadOnly("cmi.core.total_time", cmi => cmi.TotalTime);
        ReadOnly("cmi.core.lesson_mode", cmi => cmi.LessonMode);
        WriteOnly("cmi.core.exit", (cmi, _, value) => cmi.Exit = value, value => _exits.Contains(value, StringComparer.Ordinal));
        WriteOnly("cmi.core.session_time", (cmi, _, value) => cmi.SessionTime = value, IsTimespan);

        ReadWrite("cmi.suspend_data", (cmi, _) => cmi.SuspendData, (cmi, _, value) => cmi.SuspendData = value, value => IsString(value, 4096));
        ReadOnly("cmi.launch_data", cmi => cmi.LaunchData);
        ReadWrite("cmi.comments", (cmi, _) => cmi.Comments, (cmi, _, value) => cmi.Comments = value, value => IsString(value, 4096));
        ReadOnly("cmi.comments_from_lms", cmi => cmi.CommentsFromLms);

        Keyword("cmi.objectives._children", (_, _) => "id,score,status");
        Keyword("cmi.objectives._count", (cmi, _) => cmi.Objectives.Count.ToString(CultureInfo.InvariantCulture));
        ReadWrite("cmi.objectives.n.id", (cmi, i) => cmi.Objectives[i[0]].Id, (cmi, i, value) => Objective(cmi, i[0]).Id = value, IsIdentifier);
        Keyword("cmi.objectives.n.score._children", (_, _) => ScoreChildren);
        ReadWrite("cmi.objectives.n.score.raw", (cmi, i) => cmi.Objectives[i[0]].ScoreRaw, (cmi, i, value) => Objective(cmi, i[0]).ScoreRaw = value, IsScore);
        ReadWrite("cmi.objectives.n.score.min", (cmi, i) => cmi.Objectives[i[0]].ScoreMin, (cmi, i, value) => Objective(cmi, i[0]).ScoreMin = value, IsScore);
        ReadWrite("cmi.objectives.n.score.max", (cmi, i) => cmi.Objectives[i[0]].ScoreMax, (cmi, i, value) => Objective(cmi, i[0]).ScoreMax = value, IsScore);
        ReadWrite("cmi.objectives.n.status", (cmi, i) => cmi.Objectives[i[0]].Status, (cmi, i, value) => Objective(cmi, i[0]).Status = value, value => _objectiveStatuses.Contains(value, StringComparer.Ordinal));

        Keyword("cmi.student_data._children", (_, _) => "mastery_score,max_time_allowed,time_limit_action");
        ReadOnly("cmi.student_data.mastery_score", cmi => cmi.MasteryScore);
        ReadOnly("cmi.student_data.max_time_allowed", cmi => cmi.MaxTimeAllowed);
        ReadOnly("cmi.student_data.time_limit_action", cmi => cmi.TimeLimitAction);

        Keyword("cmi.student_preference._children", (_, _) => "audio,language,speed,text");
        ReadWrite("cmi.student_preference.audio", (cmi, _) => cmi.Audio, (cmi, _, value) => cmi.Audio = value, value => IsInteger(value, -1, 100));
        ReadWrite("cmi.student_preference.language", (cmi, _) => cmi.Language, (cmi, _, value) => cmi.Language = value, value => IsString(value, 255));
        ReadWrite("cmi.student_preference.speed", (cmi, _) => cmi.Speed, (cmi, _, value) => cmi.Speed = value, value => IsInteger(value, -100, 100));
        ReadWrite("cmi.student_preference.text", (cmi, _) => cmi.Text, (cmi, _, value) => cmi.Text = value, value => IsInteger(value, -1, 1));

        Keyword("cmi.interactions._children", (_, _) => "id,objectives,time,type,correct_responses,weighting,student_response,result,latency");
        Keyword("cmi.interactions._count", (cmi, _) => cmi.Interactions.Count.ToString(CultureInfo.InvariantCulture));
        WriteOnly("cmi.interactions.n.id", (cmi, i, value) => Interaction(cmi, i[0]).Id = value, IsIdentifier);
        Keyword("cmi.interactions.n.objectives._count", (cmi, i) => (i[0] < cmi.Interactions.Count ? cmi.Interactions[i[0]].ObjectiveIds.Count : 0).ToString(CultureInfo.InvariantCulture));
        WriteOnly("cmi.interactions.n.objectives.n.id", (cmi, i, value) => SetAt(Interaction(cmi, i[0]).ObjectiveIds, i[1], value), IsIdentifier);
        WriteOnly("cmi.interactions.n.time", (cmi, i, value) => Interaction(cmi, i[0]).Time = value, IsTime);
        WriteOnly("cmi.interactions.n.type", (cmi, i, value) => Interaction(cmi, i[0]).Type = value, value => _interactionTypes.Contains(value, StringComparer.Ordinal));
        Keyword("cmi.interactions.n.correct_responses._count", (cmi, i) => (i[0] < cmi.Interactions.Count ? cmi.Interactions[i[0]].CorrectResponses.Count : 0).ToString(CultureInfo.InvariantCulture));
        WriteOnly("cmi.interactions.n.correct_responses.n.pattern", (cmi, i, value) => SetAt(Interaction(cmi, i[0]).CorrectResponses, i[1], value), value => IsString(value, 255));
        WriteOnly("cmi.interactions.n.weighting", (cmi, i, value) => Interaction(cmi, i[0]).Weighting = value, IsDecimal);
        WriteOnly("cmi.interactions.n.student_response", (cmi, i, value) => Interaction(cmi, i[0]).StudentResponse = value, value => IsString(value, 255));
        WriteOnly("cmi.interactions.n.result", (cmi, i, value) => Interaction(cmi, i[0]).Result = value, value => _results.Contains(value, StringComparer.Ordinal) || IsDecimal(value));
        WriteOnly("cmi.interactions.n.latency", (cmi, i, value) => Interaction(cmi, i[0]).Latency = value, IsTimespan);

        return elements;
    }

    // Every element and every category above one: the parents a _children or _count may be asked of.
    private static HashSet<string> BuildKnown()
    {
        var known = new HashSet<string>(StringComparer.Ordinal);

        foreach (var pattern in _elements.Keys)
        {
            var segments = pattern.Split('.');

            for (var length = 1; length <= segments.Length; length++)
            {
                var prefix = string.Join('.', segments.Take(length));

                if (!prefix.EndsWith("._children", StringComparison.Ordinal) && !prefix.EndsWith("._count", StringComparison.Ordinal) && !prefix.EndsWith("._version", StringComparison.Ordinal))
                {
                    known.Add(prefix);
                }
            }
        }

        // "cmi" itself has children and is no array, but it is not an element a content reads.
        known.Remove("cmi");

        return known;
    }

    private static bool IsString(string value, int maxLength)
        => value.Length <= maxLength;

    private static bool IsScore(string value)
        => value.Length == 0 || (IsDecimal(value) && decimal.TryParse(value, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number) && number is >= 0 and <= 100);

    private static bool IsDecimal(string value)
        => DecimalPattern().IsMatch(value);

    private static bool IsInteger(string value, int min, int max)
        => IntegerPattern().IsMatch(value) && int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number) && number >= min && number <= max;

    private static bool IsIdentifier(string value)
        => IdentifierPattern().IsMatch(value);

    /// <summary>
    /// Gets whether a value is a CMITimespan: <c>HHHH:MM:SS.SS</c>, with two to four digits of hours and an optional
    /// one or two digits of hundredths, minutes and seconds below 60.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    internal static bool IsTimespan(string value)
    {
        var match = TimespanPattern().Match(value ?? string.Empty);

        return match.Success && int.Parse(match.Groups["m"].Value, CultureInfo.InvariantCulture) < 60 && int.Parse(match.Groups["s"].Value, CultureInfo.InvariantCulture) < 60;
    }

    private static bool IsTime(string value)
    {
        var match = TimePattern().Match(value);

        return match.Success &&
            int.Parse(match.Groups["h"].Value, CultureInfo.InvariantCulture) < 24 &&
            int.Parse(match.Groups["m"].Value, CultureInfo.InvariantCulture) < 60 &&
            int.Parse(match.Groups["s"].Value, CultureInfo.InvariantCulture) < 60;
    }

    [GeneratedRegex("^-?([0-9]+(\\.[0-9]*)?|\\.[0-9]+)$", RegexOptions.CultureInvariant)]
    private static partial Regex DecimalPattern();

    [GeneratedRegex("^-?[0-9]+$", RegexOptions.CultureInvariant)]
    private static partial Regex IntegerPattern();

    [GeneratedRegex("^[\\u0021-\\u007E]{1,255}$", RegexOptions.CultureInvariant)]
    private static partial Regex IdentifierPattern();

    [GeneratedRegex("^(?<h>[0-9]{2,4}):(?<m>[0-9]{2}):(?<s>[0-9]{2})(\\.[0-9]{1,2})?$", RegexOptions.CultureInvariant)]
    private static partial Regex TimespanPattern();

    [GeneratedRegex("^(?<h>[0-9]{2}):(?<m>[0-9]{2}):(?<s>[0-9]{2})(\\.[0-9]{1,2})?$", RegexOptions.CultureInvariant)]
    private static partial Regex TimePattern();

    private enum Access
    {
        Keyword,
        ReadOnly,
        WriteOnly,
        ReadWrite,
    }

    private sealed record Element(Access Access, Func<Scorm12Cmi, int[], string> Get, Action<Scorm12Cmi, int[], string> Set, Func<string, bool> IsValid);
}

/// <summary>
/// A value read from the data model.
/// </summary>
/// <param name="Value">The value, empty on an error.</param>
/// <param name="Error">The error code.</param>
public readonly record struct Scorm12Read(string Value, string Error);
