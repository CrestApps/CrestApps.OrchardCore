using CrestApps.Core.AI;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Supplies the transcript of a call an automated voice agent talked on: the lines of its AI conversation, placed on
/// the recording's timeline.
/// </summary>
/// <remarks>
/// The conversation is the recording's own <see cref="CallRecording.AiSessionId"/> when it names one, otherwise the AI
/// session of the CRM activity the call belongs to. The assistant's lines are the AI's and the user's lines are the
/// customer's. A person who takes the call over is not transcribed, so no line is attributed to an agent.
/// </remarks>
public sealed class AIConversationCallRecordingTranscriptProvider : ICallRecordingTranscriptProvider
{
    // The marker the voice agent appends to its closing line to end the call. It is never spoken.
    private const string HangupMarker = "[[HANGUP]]";

    private readonly IAIChatSessionPromptStore _promptStore;
    private readonly IOmnichannelActivityManager _activityManager;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIConversationCallRecordingTranscriptProvider"/> class.
    /// </summary>
    /// <param name="promptStores">The AI conversation store, registered only while an AI chat feature is enabled.</param>
    /// <param name="activityManager">The CRM activity manager, read to find the call's AI session.</param>
    /// <param name="logger">The logger.</param>
    public AIConversationCallRecordingTranscriptProvider(
        IEnumerable<IAIChatSessionPromptStore> promptStores,
        IOmnichannelActivityManager activityManager,
        ILogger<AIConversationCallRecordingTranscriptProvider> logger)
    {
        _promptStore = promptStores.LastOrDefault();
        _activityManager = activityManager;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<CallRecordingTranscript> GetTranscriptAsync(CallRecording recording, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recording);

        if (_promptStore is null)
        {
            return null;
        }

        var sessionId = recording.AiSessionId;

        if (string.IsNullOrEmpty(sessionId) && !string.IsNullOrEmpty(recording.ActivityItemId))
        {
            var activity = await _activityManager.FindByIdAsync(recording.ActivityItemId, cancellationToken);

            sessionId = activity?.AISessionId;
        }

        if (string.IsNullOrEmpty(sessionId))
        {
            return null;
        }

        var prompts = await _promptStore.GetPromptsAsync(sessionId);
        var lines = (prompts ?? [])
            .Where(prompt => !prompt.IsGeneratedPrompt && (prompt.Role == ChatRole.Assistant || prompt.Role == ChatRole.User))
            .Select(prompt => new CallRecordingTranscriptLine(
                prompt.Role == ChatRole.Assistant ? CallRecordingTranscriptSpeaker.Ai : CallRecordingTranscriptSpeaker.Customer,
                (prompt.Content ?? string.Empty).Replace(HangupMarker, string.Empty, StringComparison.Ordinal),
                prompt.CreatedUtc))
            .ToList();

        if (lines.Count == 0)
        {
            return null;
        }

        var transcript = CallRecordingTimeline.Build(recording, lines);

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "Placed {LineCount} line(s) of AI session '{SessionId}' on call recording '{RecordingId}'; {OnRecording} fall on the recording, with {Silence} s of silence.",
                transcript.Phrases.Count,
                sessionId.SanitizeLogValue(),
                recording.ItemId.SanitizeLogValue(),
                transcript.Phrases.Count(phrase => phrase.OffsetSeconds.HasValue),
                transcript.TotalSilenceSeconds);
        }

        return transcript;
    }
}
