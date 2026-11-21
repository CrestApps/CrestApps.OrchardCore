using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Packages.Models;
using CrestApps.OrchardCore.Learning.Xapi.Core;
using CrestApps.OrchardCore.Learning.Xapi.Core.Validation;

namespace CrestApps.OrchardCore.Learning.Packages.Core.Cmi5;

/// <summary>
/// The cmi5 rules for the statements an AU sends (cmi5 Quartz section 9): every statement carries the context template
/// of its session (the registration, the session id and the publisher's AU id in the grouping); <c>initialized</c> comes
/// first and once; nothing comes after <c>terminated</c> or after the LMS abandoned the session; a cmi5 defined statement
/// (with the cmi5 category) uses an AU verb about the AU itself; <c>completed</c> (result.completion true), <c>passed</c>
/// (result.success true) and <c>failed</c> (result.success false) need the Normal launch mode and the moveOn category,
/// <c>completed</c> and <c>passed</c> come once per registration and <c>failed</c> not after <c>passed</c>; with a mastery
/// score, <c>passed</c> needs a scaled score at least that and <c>failed</c> a scaled score below it. A statement that
/// breaks a rule is refused with 403.
/// </summary>
public static class Cmi5Rules
{
    /// <summary>
    /// Checks a statement against the cmi5 rules, and applies it to the state when it holds.
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <param name="state">The cmi5 state of the registration, changed when the statement holds.</param>
    /// <param name="course">The course.</param>
    /// <param name="now">The time, in UTC.</param>
    /// <returns>Why the statement breaks the rules, or <see langword="null"/>.</returns>
    public static string CheckAndApply(JsonObject statement, Cmi5State state, Cmi5Course course, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(statement);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(course);

        var context = statement["context"] as JsonObject;
        var sessionId = XapiAgents.Text(context?["extensions"]?[Cmi5Vocabulary.Extensions.SessionId]);
        var verb = XapiAgents.Text(statement["verb"]?["id"]);
        var isCmi5 = HasActivity(context, "category", Cmi5Vocabulary.Categories.Cmi5);

        if (isCmi5 && Array.IndexOf(Cmi5Vocabulary.LmsVerbs, verb) >= 0)
        {
            return $"Only the LMS sends {verb} in a cmi5 statement.";
        }

        if (string.IsNullOrEmpty(sessionId))
        {
            return "A cmi5 statement must carry the session id of its launch (the context template of LMS.LaunchData).";
        }

        var session = state.Sessions.FirstOrDefault(item => item.SessionId == sessionId);

        if (session is null)
        {
            return "The session of the statement is not a launch of this registration.";
        }

        if (session.TerminatedUtc is not null)
        {
            return "The session sent terminated; no statement may follow it.";
        }

        if (session.AbandonedUtc is not null)
        {
            return "The session was abandoned; no statement may follow it.";
        }

        if (session.ExpiresUtc <= now)
        {
            return "The session has expired.";
        }

        if (!HasActivity(context, "grouping", session.AuId))
        {
            return "A cmi5 statement must carry the AU's id in its grouping (the context template of LMS.LaunchData).";
        }

        var initialize = isCmi5 && verb == Cmi5Vocabulary.Verbs.Initialized;

        if (session.InitializedUtc is null && !initialize)
        {
            return "initialized must be the first statement of a session.";
        }

        if (session.InitializedUtc is not null && initialize)
        {
            return "initialized may be sent once a session.";
        }

        var au = course.Aus.FirstOrDefault(item => item.Id == session.AuId);
        var auState = state.Aus.FirstOrDefault(item => item.AuId == session.AuId);

        if (au is null || auState is null)
        {
            return "The AU of the session is not in the course.";
        }

        if (!isCmi5)
        {
            // A cmi5 allowed statement: the context template and the session's order are all it must follow.
            return null;
        }

        if (Array.IndexOf(Cmi5Vocabulary.AuVerbs, verb) < 0)
        {
            return $"{verb} is not a verb an AU may send in a cmi5 statement.";
        }

        if (!string.Equals(XapiAgents.Text(statement["object"]?["id"]), session.ActivityId, StringComparison.Ordinal))
        {
            return "The object of a cmi5 statement must be the AU's activity id.";
        }

        var result = statement["result"] as JsonObject;

        switch (verb)
        {
            case Cmi5Vocabulary.Verbs.Completed:
            {
                var error = CheckMoveOnStatement(context, session)
                    ?? (Flag(result?["completion"]) != true ? "completed must have result.completion true." : null)
                    ?? (auState.Completed ? "completed may be sent once a registration." : null);

                if (error is not null)
                {
                    return error;
                }

                auState.Completed = true;
                break;
            }

            case Cmi5Vocabulary.Verbs.Passed:
            {
                var scaled = Scaled(result);
                var error = CheckMoveOnStatement(context, session)
                    ?? (Flag(result?["success"]) != true ? "passed must have result.success true." : null)
                    ?? (auState.Passed ? "passed may be sent once a registration." : null)
                    ?? (au.MasteryScore is { } mastery && (scaled is null || scaled < mastery) ? $"passed needs result.score.scaled of at least the mastery score {mastery}." : null);

                if (error is not null)
                {
                    return error;
                }

                auState.Passed = true;
                auState.Failed = false;
                auState.ScoreScaled = scaled ?? auState.ScoreScaled;
                break;
            }

            case Cmi5Vocabulary.Verbs.Failed:
            {
                var scaled = Scaled(result);
                var error = CheckMoveOnStatement(context, session)
                    ?? (Flag(result?["success"]) != false ? "failed must have result.success false." : null)
                    ?? (auState.Passed ? "failed may not follow passed in a registration." : null)
                    ?? (au.MasteryScore is { } mastery && (scaled is null || scaled >= mastery) ? $"failed needs result.score.scaled below the mastery score {mastery}." : null);

                if (error is not null)
                {
                    return error;
                }

                auState.Failed = true;
                auState.ScoreScaled = scaled ?? auState.ScoreScaled;
                break;
            }

            case Cmi5Vocabulary.Verbs.Initialized:
                session.InitializedUtc = now;
                break;

            case Cmi5Vocabulary.Verbs.Terminated:
                session.TerminatedUtc = now;
                break;
        }

        return null;
    }

    /// <summary>
    /// Gets whether a statement is cmi5 defined: it has the cmi5 category.
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <returns><see langword="true"/> for a cmi5 defined statement.</returns>
    public static bool IsCmi5Defined(JsonObject statement)
        => HasActivity(statement?["context"] as JsonObject, "category", Cmi5Vocabulary.Categories.Cmi5);

    private static string CheckMoveOnStatement(JsonObject context, Cmi5Session session)
    {
        if (!string.Equals(session.LaunchMode, "Normal", StringComparison.Ordinal))
        {
            return $"completed, passed and failed may only be sent in the Normal launch mode; this session is {session.LaunchMode}.";
        }

        return HasActivity(context, "category", Cmi5Vocabulary.Categories.MoveOn)
            ? null
            : "completed, passed and failed must carry the moveOn category.";
    }

    private static bool HasActivity(JsonObject context, string kind, string id)
    {
        var activities = context?["contextActivities"]?[kind];
        var list = activities is JsonArray array ? array.ToList() : [activities];

        return list.Any(activity => string.Equals(XapiAgents.Text(activity?["id"]), id, StringComparison.Ordinal));
    }

    private static bool? Flag(JsonNode node)
        => node is JsonValue value && value.TryGetValue<bool>(out var flag) ? flag : null;

    private static decimal? Scaled(JsonObject result)
        => XapiStatementValidator.TryGetNumber(result?["score"]?["scaled"], out var scaled) ? scaled : null;
}
