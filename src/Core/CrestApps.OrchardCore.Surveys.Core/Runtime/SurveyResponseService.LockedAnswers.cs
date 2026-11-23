using System.Text.Json;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Core.Runtime;

// Keeps locked answers on the respondent's path. With immediate feedback a checked answer is locked and always counts
// toward the score, so a respondent who could change an earlier answer afterwards, to skip the locked answer's page or
// hide its question, could add the locked answers of a branch they then left on top of the branch they finish. A write
// whose answers would take a locked answer off the path or hide it is refused instead: nothing is saved, and the error
// goes on the answers that would do it. Only what the respondent posts is refused. The author's own logic (a rule on
// the date or time, say) can still hide a locked answer without any change of the respondent's, and that answer keeps
// counting, because the respondent saw it graded and can't do anything about it.
public sealed partial class SurveyResponseService
{
    // Gets whether a write to a response could break a lock: the survey checks answers one at a time and the response has
    // a locked answer. A check that locks the first answer can't break anything: the answer it locks is on the page shown.
    private static bool GuardsLocks(SurveyDefinition definition, SurveyResponse response)
        => definition?.Scoring?.FeedbackMode == FeedbackMode.Immediate
            && response.Answers.Values.Any(answer => answer?.CheckedUtc is not null);

    // A copy of the response before a write that could break a lock, to compare with and to put back; otherwise null.
    private static SurveyResponse LockSnapshot(SurveyDefinition definition, SurveyResponse response)
        => GuardsLocks(definition, response) ? Clone(response) : null;

    // Puts back the answers and variables a refused write changed.
    private static void RestoreSnapshot(SurveyResponse response, SurveyResponse snapshot)
    {
        response.Answers = snapshot.Answers;
        response.Variables = snapshot.Variables;
    }

    // Finds the locked answers a write takes off the respondent's path or hides, comparing the response before the write
    // with the response after it. Returns null when it keeps them all; otherwise the error by question: on each posted
    // question whose new answer alone does it, else on every posted question it changed. A locked answer the response
    // had already lost before the write (to the author's logic) is not the write's doing and is left alone.
    private Dictionary<string, List<string>> FindLockBreaks(SurveyDefinition definition, string cacheKey, SurveyResponse before, SurveyResponse after, IEnumerable<string> postedQuestionIds)
    {
        if (before is null || definition?.Scoring?.FeedbackMode != FeedbackMode.Immediate)
        {
            return null;
        }

        var locked = after.Answers.Where(pair => pair.Value?.CheckedUtc is not null).Select(pair => pair.Key).ToList();

        if (locked.Count == 0)
        {
            return null;
        }

        var keptBefore = KeptLocks(definition, cacheKey, Clone(before), locked);
        var keptAfter = KeptLocks(definition, cacheKey, Clone(after), locked);
        var broken = keptBefore.Where(questionId => !keptAfter.Contains(questionId)).ToList();

        if (broken.Count == 0)
        {
            return null;
        }

        var changed = postedQuestionIds
            .Distinct(StringComparer.Ordinal)
            .Where(questionId => !SameAnswer(before.Answers.GetValueOrDefault(questionId), after.Answers.GetValueOrDefault(questionId)))
            .ToList();

        var culprits = changed
            .Where(questionId =>
            {
                // The write with only this answer put back: when that keeps every broken lock, this answer breaks them.
                var probe = Clone(after);

                if (before.Answers.TryGetValue(questionId, out var earlier) && earlier is not null)
                {
                    probe.Answers[questionId] = JsonSerializer.Deserialize<SurveyAnswer>(JsonSerializer.Serialize(earlier, _cloneOptions), _cloneOptions);
                }
                else
                {
                    probe.Answers.Remove(questionId);
                }

                var kept = KeptLocks(definition, cacheKey, probe, broken);

                return broken.All(kept.Contains);
            })
            .ToList();

        if (culprits.Count == 0)
        {
            culprits = changed.Count > 0 ? changed : broken;
        }

        var message = S["You can't change this answer now: the change would skip or hide a question whose answer you already checked. Keep your earlier answer to go on."].Value;

        return culprits.ToDictionary(questionId => questionId, _ => new List<string> { message }, StringComparer.Ordinal);
    }

    // Walks a response from its first screen to its end with its answers as they are, choosing each next screen as a page
    // post does (page visibility, branches, and the answers a branch skips dropped, unless locked), and returns the locked
    // questions that are on a screen of that path and that logic does not hide at its end. The response is changed.
    private HashSet<string> KeptLocks(SurveyDefinition definition, string cacheKey, SurveyResponse response, IReadOnlyCollection<string> lockedIds)
    {
        var plan = response.Plan;
        var screens = new HashSet<string>(StringComparer.Ordinal);

        _formulas.ComputeVariables(definition, response, cacheKey);

        var state = _logic.Evaluate(definition, response, cacheKey);
        var screen = SurveyPlanNavigator.FirstShowing(plan, state);

        // A next screen is always further on in the plan, so the walk ends; the set stops it on a bad plan too.
        while (screen is not null && screens.Add(screen))
        {
            var step = ChooseNext(definition, response, state, cacheKey, screen);

            if (step.SkipTo > step.SkipFrom)
            {
                SkipScreens(response, step);
                _formulas.ComputeVariables(definition, response, cacheKey);
                state = _logic.Evaluate(definition, response, cacheKey);
            }

            screen = step.Next;
        }

        var screenOf = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (screenId, questionIds) in plan.QuestionOrder ?? [])
        {
            foreach (var questionId in questionIds ?? [])
            {
                screenOf.TryAdd(questionId, screenId);
            }
        }

        var kept = new HashSet<string>(StringComparer.Ordinal);

        foreach (var questionId in lockedIds)
        {
            // A locked question the plan doesn't hold (a response without a plan) has no path to leave.
            if (!state.HiddenQuestionIds.Contains(questionId)
                && (!screenOf.TryGetValue(questionId, out var screenId) || screens.Contains(screenId)))
            {
                kept.Add(questionId);
            }
        }

        return kept;
    }

    // Whether two answers hold the same values (when they were given, checked or scored aside).
    private static bool SameAnswer(SurveyAnswer first, SurveyAnswer second)
    {
        if (first is null || second is null)
        {
            return first is null && second is null;
        }

        return string.Equals(JsonSerializer.Serialize(Values(first), _cloneOptions), JsonSerializer.Serialize(Values(second), _cloneOptions), StringComparison.Ordinal);

        static object Values(SurveyAnswer answer)
            => new { answer.Values, answer.Number, answer.Date, answer.Rows, answer.OtherText, answer.AttachmentIds, answer.Extra };
    }
}
