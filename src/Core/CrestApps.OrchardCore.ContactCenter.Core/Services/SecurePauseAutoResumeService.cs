using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony.Models;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="ISecurePauseAutoResumeService"/>.
/// </summary>
public sealed class SecurePauseAutoResumeService : ISecurePauseAutoResumeService
{
    private const int MaxResumeBatchSize = 200;

    private readonly ISiteService _siteService;
    private readonly IInteractionManager _interactionManager;
    private readonly IContactCenterRecordingService _recordingService;
    private readonly IEnumerable<IContactCenterRealTimeNotifier> _realTimeNotifiers;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="SecurePauseAutoResumeService"/> class.
    /// </summary>
    /// <param name="siteService">The site service used to read the tenant recording governance settings.</param>
    /// <param name="interactionManager">The interaction manager used to list expired pauses.</param>
    /// <param name="recordingService">The recording orchestration service that resumes recording.</param>
    /// <param name="realTimeNotifiers">The optional real-time notifiers used to broadcast the resume.</param>
    /// <param name="clock">The clock used to compute the pause-expiry cutoff.</param>
    public SecurePauseAutoResumeService(
        ISiteService siteService,
        IInteractionManager interactionManager,
        IContactCenterRecordingService recordingService,
        IEnumerable<IContactCenterRealTimeNotifier> realTimeNotifiers,
        IClock clock)
    {
        _siteService = siteService;
        _interactionManager = interactionManager;
        _recordingService = recordingService;
        _realTimeNotifiers = realTimeNotifiers;
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task<int> ResumeExpiredAsync(CancellationToken cancellationToken = default)
    {
        var site = await _siteService.GetSiteSettingsAsync();
        var settings = site.GetOrCreate<ContactCenterRecordingSettings>();

        // Clamp defensively at enforcement time as well as in the settings UI: a persisted value written by a
        // recipe, import, or older build must never widen the pause window past the one-day ceiling.
        var maxSecurePauseSeconds = Math.Clamp(settings.MaxSecurePauseSeconds, 0, ContactCenterRecordingSettings.MaxSecurePauseSecondsLimit);

        // A non-positive window disables the guard: the tenant has chosen that a secure pause persists until it is
        // explicitly resumed, so there is nothing to force-resume.
        if (maxSecurePauseSeconds <= 0)
        {
            return 0;
        }

        var cutoffUtc = _clock.UtcNow.AddSeconds(-maxSecurePauseSeconds);

        var expired = await _interactionManager.ListPausedRecordingsOlderThanAsync(cutoffUtc, MaxResumeBatchSize, cancellationToken);

        if (expired.Count == 0)
        {
            return 0;
        }

        var resumed = 0;

        foreach (var interaction in expired)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await _recordingService.AutoResumeAsync(interaction.ItemId, cancellationToken);

            if (!result.Succeeded)
            {
                continue;
            }

            resumed++;

            await NotifyAsync(interaction, cancellationToken);
        }

        return resumed;
    }

    private async Task NotifyAsync(Interaction interaction, CancellationToken cancellationToken)
    {
        var notifier = _realTimeNotifiers.FirstOrDefault();

        if (notifier is null)
        {
            return;
        }

        await notifier.NotifyRecordingStateChangedAsync(new RecordingStateNotification
        {
            InteractionId = interaction.ItemId,
            AgentId = interaction.AgentId,
            RecordingState = RecordingState.Recording.ToString(),
            IsSecurePauseActive = false,
            ServerTimeUtc = _clock.UtcNow,
        }, cancellationToken);
    }
}
