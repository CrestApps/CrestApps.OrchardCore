using CrestApps.Core.Omnichannel.Models;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;

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
