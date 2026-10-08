using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// Decides what a finished automated call is allowed to claim happened on it.
/// </summary>
/// <remarks>
/// The call review is an LLM reading the transcript. Asked to review an empty one it does not answer "nothing
/// happened" — it writes a fluent, plausible account of a conversation that never took place, and that account is
/// saved to the CRM as fact and read later by a person who has no way to tell. A real call that nobody spoke on
/// was recorded as "the customer expressed interest in a vehicle but did not specify the type, budget, or
/// timeline", every word invented.
/// </remarks>
public static class VoiceCallConclusionPolicy
{
    /// <summary>
    /// The note written for a call on which nothing was said.
    /// </summary>
    public const string NoConversationNote = "The automated call produced no conversation: nothing was said by either side.";

    /// <summary>
    /// The note written for a call that did have a conversation but which the review did not summarize.
    /// </summary>
    public const string CompletedWithoutSummaryNote = "Automated AI voice call completed.";

    /// <summary>
    /// The note written for a call that was answered by voicemail rather than by the customer.
    /// </summary>
    public const string VoicemailNote = "The automated call reached voicemail and left a message; nobody spoke with the customer.";

    /// <summary>
    /// The note written for a call that found the customer's line busy.
    /// </summary>
    public const string BusyNote = "The automated call found the line busy; nobody spoke with the customer.";

    /// <summary>
    /// The note written for a call whose assistant lost its live session partway through the conversation.
    /// </summary>
    public const string SessionLostNote = "The automated assistant lost its connection partway through the call, so the conversation did not finish.";

    /// <summary>
    /// The terminal reason recorded on a call whose assistant lost its live session, so reports can tell a call
    /// our side dropped from one the customer ended.
    /// </summary>
    public const string SessionLostReasonCode = "ai_session_lost";

    /// <summary>
    /// Whether this call's outcome is the automation's to write.
    /// </summary>
    /// <remarks>
    /// The model's leg ending is not the same as the call ending. When the caller has been handed to a live
    /// agent the model disconnects immediately, and concluding on that would close and disposition the activity
    /// while the agent is still talking — the outcome on record would be the model's guess rather than what the
    /// agent did, and the agent's own wrap-up would then be refused because the work was already finished. An
    /// escalated call belongs to the agent who took it; if nobody ever picks it up, the recovery sweeps close it.
    /// </remarks>
    /// <param name="activity">The activity behind the call.</param>
    public static bool ShouldConclude(OmnichannelActivity activity)
        => activity is not null &&
            !activity.AiEscalated &&
            !activity.Status.IsTerminal();

    /// <summary>
    /// Whether anybody actually said anything on the call.
    /// </summary>
    /// <remarks>
    /// Generated prompts are the scaffolding the platform adds rather than speech, and an empty or whitespace turn
    /// is a transcription that produced nothing — neither is somebody talking.
    /// </remarks>
    /// <param name="prompts">The stored transcript for the call's session.</param>
    public static bool HasConversation(IEnumerable<AIChatSessionPrompt> prompts)
        => prompts is not null
            && prompts.Any(prompt => !prompt.IsGeneratedPrompt && !string.IsNullOrWhiteSpace(prompt.Content));

    /// <summary>
    /// The notes to record against the concluded call.
    /// </summary>
    /// <param name="hasConversation">Whether anything was said, from <see cref="HasConversation"/>.</param>
    /// <param name="modelSummary">What the review wrote, when it ran and produced anything.</param>
    public static string ResolveNotes(bool hasConversation, string modelSummary)
    {
        if (!hasConversation)
        {
            // Deliberately ignores any summary offered for a silent call: there was nothing to summarize, so
            // anything on offer was invented.
            return NoConversationNote;
        }

        return string.IsNullOrWhiteSpace(modelSummary)
            ? CompletedWithoutSummaryNote
            : modelSummary;
    }

    /// <summary>
    /// The dispositions the subject's own workflow allows, as the identifiers its actions are wired to.
    /// </summary>
    /// <remarks>
    /// An automated call is classified by the model, but not from the whole catalog: what a call about this
    /// subject is allowed to conclude as is exactly what the subject's actions can act on, so a disposition the
    /// model picks always has somewhere to lead. An empty result means the subject has no actions configured,
    /// and the caller falls back to every disposition rather than leaving the call unclassified.
    /// </remarks>
    /// <param name="subjectActions">Every configured subject action.</param>
    /// <param name="subjectContentType">The subject content type of the call being concluded.</param>
    public static IReadOnlyList<string> ResolveSubjectDispositionIds(
        IEnumerable<SubjectAction> subjectActions,
        string subjectContentType)
    {
        if (subjectActions is null || string.IsNullOrEmpty(subjectContentType))
        {
            return [];
        }

        return subjectActions
            .Where(action => action is not null &&
                string.Equals(action.SubjectContentType, subjectContentType, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrEmpty(action.DispositionId))
            .Select(action => action.DispositionId)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// The disposition to record for a call nobody spoke on.
    /// </summary>
    /// <remarks>
    /// There is no conversation for the model to judge, so it is not asked, and taking the first outcome on offer
    /// instead recorded a call that rang out unanswered as "Done" -- finished, and never tried again. A disposition
    /// marked with the matching outcome says what to record: a voicemail takes the answering-machine one, a busy line
    /// the busy one, and a call nobody answered (or either of those with no disposition of its own) the no-answer one.
    /// A subject with none of them takes the outcome its try-again action is wired to, so the call is tried again, and
    /// failing that the first choice, as before.
    /// </remarks>
    /// <param name="choices">The dispositions the call may be concluded as.</param>
    /// <param name="subjectActions">Every configured subject action.</param>
    /// <param name="subjectContentType">The subject content type of the call being concluded.</param>
    /// <param name="reachedVoicemail">Whether a voicemail, rather than nobody, picked up.</param>
    /// <param name="lineBusy">Whether the network reported the customer's line busy.</param>
    public static OmnichannelDisposition ChooseUnansweredDisposition(
        IEnumerable<OmnichannelDisposition> choices,
        IEnumerable<SubjectAction> subjectActions,
        string subjectContentType,
        bool reachedVoicemail = false,
        bool lineBusy = false)
    {
        var offered = choices as IList<OmnichannelDisposition> ?? choices?.ToList();

        if (offered is null || offered.Count == 0)
        {
            return null;
        }

        // The choices are already the subject's own dispositions (or every disposition when it wires none), so any of
        // them with the outcome will do.
        var specific = reachedVoicemail
            ? DispositionOutcome.AnsweringMachine
            : lineBusy ? DispositionOutcome.Busy : DispositionOutcome.None;

        var byOutcome = DispositionOutcomes.Find(offered, subjectActions, subjectContentType, specific, includeUnwired: true)
            ?? DispositionOutcomes.Find(offered, subjectActions, subjectContentType, DispositionOutcome.NoAnswer, includeUnwired: true);

        return byOutcome
            ?? FindRetriedDisposition(offered, subjectActions, subjectContentType)
            ?? ChooseDisposition(offered, modelChoiceId: null);
    }

    /// <summary>
    /// The disposition to record for a call whose assistant lost its live session partway through.
    /// </summary>
    /// <remarks>
    /// The conversation was cut short by us, not ended by the customer, so what the review makes of the transcript
    /// is not an outcome: live, it read the cut-off call as a customer who never engaged and chose "Done", and the
    /// contact was never tried again. The call takes the outcome the subject's try-again action is wired to. A
    /// subject with no such action has nothing better to offer, and the review's choice stands.
    /// </remarks>
    /// <param name="choices">The dispositions the call may be concluded as.</param>
    /// <param name="subjectActions">Every configured subject action.</param>
    /// <param name="subjectContentType">The subject content type of the call being concluded.</param>
    /// <param name="modelChoiceId">The identifier the review returned, if it ran at all.</param>
    public static OmnichannelDisposition ChooseSessionLostDisposition(
        IEnumerable<OmnichannelDisposition> choices,
        IEnumerable<SubjectAction> subjectActions,
        string subjectContentType,
        string modelChoiceId)
    {
        var offered = choices as IList<OmnichannelDisposition> ?? choices?.ToList();

        if (offered is null || offered.Count == 0)
        {
            return null;
        }

        return FindRetriedDisposition(offered, subjectActions, subjectContentType)
            ?? ChooseDisposition(offered, modelChoiceId);
    }

    /// <summary>
    /// The notes to record against a call whose assistant lost its live session partway through.
    /// </summary>
    /// <param name="hasConversation">Whether anything was said, from <see cref="HasConversation"/>.</param>
    /// <param name="modelSummary">What the review wrote, when it ran and produced anything.</param>
    public static string ResolveSessionLostNotes(bool hasConversation, string modelSummary)
        => !hasConversation || string.IsNullOrWhiteSpace(modelSummary)
            ? SessionLostNote
            : SessionLostNote + " " + modelSummary.Trim();

    // The offered disposition the subject's try-again action is wired to, or null when it has none.
    /// <summary>
    /// How far ahead a callback the customer asked for may be scheduled. Further than this is a misheard date.
    /// </summary>
    public static readonly TimeSpan LongestCallbackDelay = TimeSpan.FromDays(90);

    /// <summary>
    /// The time the customer asked to be called back at, in UTC, from what the review returned.
    /// </summary>
    /// <remarks>
    /// The review reads "call me back in an hour" or "tomorrow at three" against the current time it is given and
    /// returns the moment as an ISO 8601 date and time with its offset. A value without an offset is read in the
    /// site's time zone, through <paramref name="localOffset"/>. Anything that is not a time in the future, within
    /// <see cref="LongestCallbackDelay"/>, is not used: the follow-up then takes its action's default delay rather
    /// than a moment the model invented.
    /// </remarks>
    /// <param name="requested">The time the review returned, or <see langword="null"/>.</param>
    /// <param name="nowUtc">The current time, in UTC.</param>
    /// <param name="localOffset">The site's offset from UTC, for a value given without one.</param>
    public static DateTime? ResolveCallbackUtc(string requested, DateTime nowUtc, TimeSpan localOffset)
    {
        if (string.IsNullOrWhiteSpace(requested))
        {
            return null;
        }

        var text = requested.Trim();
        DateTime utc;

        if (HasOffset(text) &&
            DateTimeOffset.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var withOffset))
        {
            utc = withOffset.UtcDateTime;
        }
        else if (DateTime.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var local))
        {
            utc = new DateTimeOffset(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), localOffset).UtcDateTime;
        }
        else
        {
            return null;
        }

        if (utc <= nowUtc || utc - nowUtc > LongestCallbackDelay)
        {
            return null;
        }

        return utc;
    }

    /// <summary>
    /// The schedule dates for the follow-ups a disposition creates, so they are due when the customer asked to be
    /// called back.
    /// </summary>
    /// <remarks>
    /// Keyed by subject action, the way an agent's completion form passes the date it was given. Only actions that
    /// create a follow-up -- trying again, or a new activity -- take a date; the rest have nothing to schedule.
    /// </remarks>
    /// <param name="subjectActions">Every configured subject action.</param>
    /// <param name="subjectContentType">The subject content type of the call being concluded.</param>
    /// <param name="dispositionId">The disposition the call is concluded as.</param>
    /// <param name="callbackUtc">When the customer asked to be called back, from <see cref="ResolveCallbackUtc"/>.</param>
    /// <returns>The dates, or <see langword="null"/> when there is nothing to schedule.</returns>
    public static Dictionary<string, DateTime?> CallbackScheduleDates(
        IEnumerable<SubjectAction> subjectActions,
        string subjectContentType,
        string dispositionId,
        DateTime? callbackUtc)
    {
        if (callbackUtc is null || string.IsNullOrEmpty(dispositionId) || subjectActions is null)
        {
            return null;
        }

        var dates = subjectActions
            .Where(action => action is not null &&
                !string.IsNullOrEmpty(action.ItemId) &&
                string.Equals(action.DispositionId, dispositionId, StringComparison.Ordinal) &&
                string.Equals(action.SubjectContentType, subjectContentType, StringComparison.OrdinalIgnoreCase) &&
                (string.Equals(action.Source, OmnichannelConstants.ActionTypes.TryAgain, StringComparison.Ordinal) ||
                 string.Equals(action.Source, OmnichannelConstants.ActionTypes.NewActivity, StringComparison.Ordinal)))
            .ToDictionary(action => action.ItemId, _ => (DateTime?)DateTime.SpecifyKind(callbackUtc.Value, DateTimeKind.Utc), StringComparer.Ordinal);

        return dates.Count > 0 ? dates : null;
    }

    // An ISO 8601 time with its offset ends in Z or in +hh:mm / -hh:mm after the time.
    private static bool HasOffset(string text)
    {
        if (text.EndsWith('Z') || text.EndsWith('z'))
        {
            return true;
        }

        var timeStart = text.IndexOf('T', StringComparison.OrdinalIgnoreCase);

        if (timeStart < 0)
        {
            timeStart = text.IndexOf(' ', StringComparison.Ordinal);
        }

        return timeStart >= 0 && text.IndexOfAny(['+', '-'], timeStart) > timeStart;
    }

    private static OmnichannelDisposition FindRetriedDisposition(
        IList<OmnichannelDisposition> offered,
        IEnumerable<SubjectAction> subjectActions,
        string subjectContentType)
    {
        var retriedDispositionIds = (subjectActions ?? [])
            .Where(action => action is not null &&
                string.Equals(action.Source, OmnichannelConstants.ActionTypes.TryAgain, StringComparison.Ordinal) &&
                string.Equals(action.SubjectContentType, subjectContentType, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrEmpty(action.DispositionId))
            .Select(action => action.DispositionId)
            .ToHashSet(StringComparer.Ordinal);

        return offered.FirstOrDefault(disposition => disposition is not null && retriedDispositionIds.Contains(disposition.ItemId));
    }

    /// <summary>
    /// The disposition to record, given what the model chose.
    /// </summary>
    /// <remarks>
    /// The model is asked for one of the choices it was shown, and is held to it: a value it invented, or one
    /// belonging to a different subject, would put an outcome on the record that the subject's workflow cannot
    /// act on. Anything outside the list falls back to the first choice rather than to nothing, because a
    /// concluded call with no disposition is invisible to every report that counts outcomes.
    /// </remarks>
    /// <param name="choices">The dispositions the model was offered.</param>
    /// <param name="modelChoiceId">The identifier the model returned, if it ran at all.</param>
    public static OmnichannelDisposition ChooseDisposition(
        IEnumerable<OmnichannelDisposition> choices,
        string modelChoiceId)
    {
        var offered = choices as IList<OmnichannelDisposition> ?? choices?.ToList();

        if (offered is null || offered.Count == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(modelChoiceId))
        {
            var chosen = offered.FirstOrDefault(disposition => disposition?.ItemId == modelChoiceId);

            if (chosen is not null)
            {
                return chosen;
            }
        }

        return offered.FirstOrDefault(disposition => disposition is not null);
    }
}
