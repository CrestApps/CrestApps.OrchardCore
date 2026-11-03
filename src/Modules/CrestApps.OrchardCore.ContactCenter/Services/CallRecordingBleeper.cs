using CrestApps.Core;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// What bleeping did to a recording, kept on it so it is done once.
/// </summary>
public sealed class CallRecordingBleepMetadata
{
    /// <summary>
    /// Gets or sets when the recording was looked at.
    /// </summary>
    public DateTime CheckedUtc { get; set; }

    /// <summary>
    /// Gets or sets how many stretches were covered with a tone.
    /// </summary>
    public int Bleeps { get; set; }

    /// <summary>
    /// Gets or sets the format the recording had before.
    /// </summary>
    public string OriginalFormat { get; set; }

    /// <summary>
    /// Gets or sets why nothing was covered, when nothing was.
    /// </summary>
    public string Skipped { get; set; }
}

/// <summary>
/// Bleeps the personal details a customer said in the recordings of AI conversations whose profile keeps
/// everything except personal details: once the conversation's transcript is redacted, each customer line that held a
/// detail is covered with a tone, and the recording is stored again as WAV in place of the original.
/// </summary>
public sealed class CallRecordingBleeper
{
    /// <summary>
    /// How far back recordings are looked at.
    /// </summary>
    public static readonly TimeSpan LookBack = TimeSpan.FromDays(14);

    private const int BatchSize = 25;

    private readonly ISession _session;
    private readonly ICallRecordingStore _recordingStore;
    private readonly IEnumerable<IRecordingMediaStore> _mediaStores;
    private readonly IEnumerable<ICallRecordingTranscriptProvider> _transcriptProviders;
    private readonly IEnumerable<IAIChatSessionManager> _sessionManagers;
    private readonly IOmnichannelActivityManager _activityManager;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CallRecordingBleeper"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="recordingStore">The recording store.</param>
    /// <param name="mediaStores">The recording media store, when one is registered.</param>
    /// <param name="transcriptProviders">The transcript providers.</param>
    /// <param name="sessionManagers">The AI session manager, when an AI chat feature is on.</param>
    /// <param name="activityManager">Finds a call's AI session through its activity.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public CallRecordingBleeper(
        ISession session,
        ICallRecordingStore recordingStore,
        IEnumerable<IRecordingMediaStore> mediaStores,
        IEnumerable<ICallRecordingTranscriptProvider> transcriptProviders,
        IEnumerable<IAIChatSessionManager> sessionManagers,
        IOmnichannelActivityManager activityManager,
        IClock clock,
        ILogger<CallRecordingBleeper> logger)
    {
        _session = session;
        _recordingStore = recordingStore;
        _mediaStores = mediaStores;
        _transcriptProviders = transcriptProviders;
        _sessionManagers = sessionManagers;
        _activityManager = activityManager;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Bleeps the recordings that are due, a batch at a time.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>How many recordings were bleeped.</returns>
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var mediaStore = _mediaStores.LastOrDefault();
        var sessionManager = _sessionManagers.LastOrDefault();

        if (mediaStore is null || sessionManager is null)
        {
            return 0;
        }

        var since = _clock.UtcNow - LookBack;
        var recordings = await _session.Query<CallRecording, CallRecordingIndex>(
                index => index.IsStored && !index.IsErased && index.StartedUtc >= since,
                collection: ContactCenterStorage.CollectionName)
            .OrderByDescending(index => index.StartedUtc)
            .Take(BatchSize * 8)
            .ListAsync(cancellationToken);

        var bleeped = 0;
        var looked = 0;

        foreach (var recording in recordings)
        {
            if (looked >= BatchSize || recording.Has<CallRecordingBleepMetadata>())
            {
                continue;
            }

            var chatSession = await FindSessionAsync(recording, sessionManager, cancellationToken);

            // The storage mode is applied a while after the conversation's last activity; until then, wait.
            if (chatSession is null || !chatSession.TryGet<AIConversationPrivacyMetadata>(out var privacy))
            {
                continue;
            }

            looked++;

            if (privacy.Mode != AIDataStorageMode.RedactPersonalData)
            {
                await MarkAsync(recording, 0, "The profile keeps everything.", cancellationToken);

                continue;
            }

            if (await BleepAsync(recording, mediaStore, cancellationToken))
            {
                bleeped++;
            }
        }

        return bleeped;
    }

    /// <summary>
    /// Bleeps one recording from its transcript.
    /// </summary>
    /// <param name="recording">The recording.</param>
    /// <param name="mediaStore">The media store.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Whether anything was covered.</returns>
    public async Task<bool> BleepAsync(CallRecording recording, IRecordingMediaStore mediaStore, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recording);
        ArgumentNullException.ThrowIfNull(mediaStore);

        var transcript = await GetTranscriptAsync(recording, cancellationToken);
        var spans = CallRecordingAudio.FindSpans(transcript, recording.DurationSeconds);

        if (spans.Count == 0)
        {
            await MarkAsync(recording, 0, transcript is null ? "No transcript." : "Nothing personal was said.", cancellationToken);

            return false;
        }

        (float[] Samples, int SampleRate)? audio;

        await using (var source = await mediaStore.OpenReadAsync(recording.StorageReference, cancellationToken))
        {
            audio = source is null ? null : CallRecordingAudio.Decode(source, recording.Format);
        }

        if (audio is not { } decoded)
        {
            await MarkAsync(recording, 0, $"The {recording.Format} audio could not be read.", cancellationToken);

            return false;
        }

        CallRecordingAudio.Bleep(decoded.Samples, decoded.SampleRate, spans);

        using var output = new MemoryStream();
        CallRecordingAudio.WriteWav(output, decoded.Samples, decoded.SampleRate);
        output.Position = 0;

        var oldReference = recording.StorageReference;
        var originalFormat = recording.Format;
        var newReference = await mediaStore.StoreAsync(new RecordingMediaWriteRequest
        {
            StorageKey = $"{recording.ItemId}-bleeped",
            InteractionId = recording.InteractionId,
            Format = "wav",
            Content = output,
        }, cancellationToken);

        recording.StorageReference = newReference;
        recording.Format = "wav";
        recording.Put(new CallRecordingBleepMetadata { CheckedUtc = _clock.UtcNow, Bleeps = spans.Count, OriginalFormat = originalFormat });
        await _recordingStore.UpdateAsync(recording, cancellationToken);
        await _session.SaveChangesAsync(cancellationToken);

        // The original goes only once the bleeped copy is saved in its place.
        if (!string.Equals(oldReference, newReference, StringComparison.Ordinal))
        {
            await mediaStore.DeleteAsync(oldReference, cancellationToken);
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Bleeped {Count} stretch(es) of call recording '{RecordingId}'.", spans.Count, recording.ItemId.SanitizeLogValue());
        }

        return true;
    }

    private async Task<AIChatSession> FindSessionAsync(CallRecording recording, IAIChatSessionManager sessionManager, CancellationToken cancellationToken)
    {
        var sessionId = recording.AiSessionId;

        if (string.IsNullOrEmpty(sessionId) && !string.IsNullOrEmpty(recording.ActivityItemId))
        {
            sessionId = (await _activityManager.FindByIdAsync(recording.ActivityItemId, cancellationToken))?.AISessionId;
        }

        return string.IsNullOrEmpty(sessionId) ? null : await sessionManager.FindByIdAsync(sessionId, cancellationToken);
    }

    private async Task<CallRecordingTranscript> GetTranscriptAsync(CallRecording recording, CancellationToken cancellationToken)
    {
        foreach (var provider in _transcriptProviders)
        {
            var transcript = await provider.GetTranscriptAsync(recording, cancellationToken);

            if (transcript?.Phrases.Count > 0)
            {
                return transcript;
            }
        }

        return null;
    }

    private async Task MarkAsync(CallRecording recording, int bleeps, string skipped, CancellationToken cancellationToken)
    {
        recording.Put(new CallRecordingBleepMetadata { CheckedUtc = _clock.UtcNow, Bleeps = bleeps, OriginalFormat = recording.Format, Skipped = skipped });
        await _recordingStore.UpdateAsync(recording, cancellationToken);
    }
}
