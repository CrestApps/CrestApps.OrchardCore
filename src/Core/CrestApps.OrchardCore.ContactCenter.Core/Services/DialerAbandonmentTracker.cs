using System.Globalization;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <inheritdoc/>
public sealed class DialerAbandonmentTracker : IDialerAbandonmentTracker
{
    // How fast the abandoned-call message is assumed to be read, in words a second, to bound how long the person may be
    // held for it. Text-to-speech reads about 150 words a minute.
    private const double WordsPerSecond = 2.5;

    // Added to the reading time: the provider takes a moment to start speaking, and the hang-up that follows the message
    // needs its own round trip. A message that ends normally is hung up long before this.
    private static readonly TimeSpan _safetyMargin = TimeSpan.FromSeconds(15);

    private static readonly TimeSpan _minimumSafetyDelay = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan _maximumSafetyDelay = TimeSpan.FromMinutes(2);

    private readonly IContactCenterEventPublisher _publisher;
    private readonly IDialerProfileReader _profileReader;
    private readonly IQueueTreatmentProvider _treatmentProvider;
    private readonly ICallSessionManager _callSessionManager;
    private readonly ISiteService _siteService;
    private readonly IShellHost _shellHost;
    private readonly ShellSettings _shellSettings;
    private readonly IClock _clock;
    private readonly ILogger _logger;
    private readonly Dictionary<string, DialerProfile> _profiles = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="DialerAbandonmentTracker"/> class.
    /// </summary>
    /// <param name="publisher">The publisher that files the answer and abandonment facts in the durable event log.</param>
    /// <param name="profileReader">The dialer profile catalog, for the call's mode and message.</param>
    /// <param name="treatmentProvider">The provider that speaks the message on the person's leg and then ends the call.</param>
    /// <param name="callSessionManager">The call sessions, for the number the call was placed from.</param>
    /// <param name="siteService">The site settings, for the name the call is made on behalf of.</param>
    /// <param name="shellHost">The shell host, used to open a scope for the safety hang-up that outlives the request.</param>
    /// <param name="shellSettings">The tenant the call belongs to.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public DialerAbandonmentTracker(
        IContactCenterEventPublisher publisher,
        IDialerProfileReader profileReader,
        IQueueTreatmentProvider treatmentProvider,
        ICallSessionManager callSessionManager,
        ISiteService siteService,
        IShellHost shellHost,
        ShellSettings shellSettings,
        IClock clock,
        ILogger<DialerAbandonmentTracker> logger)
    {
        _publisher = publisher;
        _profileReader = profileReader;
        _treatmentProvider = treatmentProvider;
        _callSessionManager = callSessionManager;
        _siteService = siteService;
        _shellHost = shellHost;
        _shellSettings = shellSettings;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<bool> RecordLiveAnswerAsync(Interaction interaction, DateTime answeredUtc, CancellationToken cancellationToken = default)
    {
        if (DialerCallMetadata.WasAnsweredByMachine(interaction))
        {
            return false;
        }

        var profile = await FindAutomatedProfileAsync(interaction, cancellationToken);

        if (profile is null)
        {
            return false;
        }

        return await MarkLiveAnsweredAsync(interaction, profile, answeredUtc, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> RecordAgentConnectedAsync(Interaction interaction, DateTime connectedUtc, CancellationToken cancellationToken = default)
    {
        var answeredUtc = DialerCallMetadata.GetLiveAnsweredUtc(interaction);

        if (answeredUtc is null || DialerCallMetadata.IsAbandoned(interaction) || connectedUtc - answeredUtc.Value <= DialerAbandonment.ConnectThreshold)
        {
            return false;
        }

        var profile = await FindAutomatedProfileAsync(interaction, cancellationToken);

        if (profile is null)
        {
            return false;
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Dialer call '{CallId}' for dialer profile '{ProfileId}' was connected to an agent {Seconds:0.0} seconds after the answer, which counts as abandoned.",
                interaction.ProviderInteractionId.SanitizeLogValue(),
                profile.ItemId.SanitizeLogValue(),
                (connectedUtc - answeredUtc.Value).TotalSeconds);
        }

        return await RecordAbandonedAsync(interaction, profile, DialerAbandonment.Reasons.AgentConnectedLate, connectedUtc, messagePlayed: false, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> RecordEndedWithoutAgentAsync(Interaction interaction, DateTime endedUtc, CancellationToken cancellationToken = default)
    {
        var answeredUtc = DialerCallMetadata.GetLiveAnsweredUtc(interaction);

        // Somebody who hangs up within the threshold was never kept waiting: the call was not abandoned, they left it.
        if (answeredUtc is null ||
            DialerCallMetadata.HasAgentJoined(interaction) ||
            DialerCallMetadata.IsAbandoned(interaction) ||
            endedUtc - answeredUtc.Value <= DialerAbandonment.ConnectThreshold)
        {
            return false;
        }

        var profile = await FindAutomatedProfileAsync(interaction, cancellationToken);

        if (profile is null)
        {
            return false;
        }

        return await RecordAbandonedAsync(interaction, profile, DialerAbandonment.Reasons.CustomerHungUpWaiting, endedUtc, messagePlayed: false, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> AbandonAsync(
        Interaction interaction,
        string providerName,
        string providerCallId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(providerCallId) || DialerCallMetadata.WasAnsweredByMachine(interaction))
        {
            return false;
        }

        var profile = await FindAutomatedProfileAsync(interaction, cancellationToken);

        if (profile is null)
        {
            return false;
        }

        var now = _clock.UtcNow;
        var message = profile.SafeHarborEnabled
            ? await RenderMessageAsync(profile, interaction, cancellationToken)
            : null;

        // Started before anything is written: the person is listening to silence until it is.
        var started = !string.IsNullOrEmpty(message) &&
            await StartMessageAsync(providerName, providerCallId, message, cancellationToken);

        await RecordAbandonedAsync(interaction, profile, reason, now, started, cancellationToken);

        if (started)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Played the abandoned-call message to {CallId} for dialer profile {ProfileId} because {Reason}.",
                    providerCallId.SanitizeLogValue(),
                    profile.ItemId.SanitizeLogValue(),
                    reason.SanitizeLogValue());
            }

            return true;
        }

        _logger.LogWarning(
            "Dialer profile {ProfileId} has no abandoned-call message that could be played, so call {CallId} is hung up without one ({Reason}).",
            profile.ItemId.SanitizeLogValue(),
            providerCallId.SanitizeLogValue(),
            reason.SanitizeLogValue());

        return false;
    }

    private async Task<bool> StartMessageAsync(string providerName, string providerCallId, string message, CancellationToken cancellationToken)
    {
        // A tenant whose provider plays nothing would leave the person on a silent line waiting for a message that
        // never comes; the caller hangs up instead.
        if (_treatmentProvider is NoQueueTreatmentProvider)
        {
            return false;
        }

        try
        {
            // The provider hangs up once the message has been spoken, and at once when it cannot speak it.
            await _treatmentProvider.EndWithMessageAsync(providerCallId, message, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "The abandoned-call message could not be started on call {CallId} (provider '{ProviderName}').",
                providerCallId.SanitizeLogValue(),
                providerName.SanitizeLogValue());

            return false;
        }

        ArmSafetyHangup(providerCallId, message);

        return true;
    }

    // The provider ends the call when it reports the message finished. Should that report never come -- the message is
    // refused after it was accepted, or the webhook is lost -- the person would be held on a silent line, so the call is
    // hung up anyway once the message has had more than enough time. Hanging up a call that already ended is harmless.
    private void ArmSafetyHangup(string providerCallId, string message)
    {
        var words = message.Split((char[])null, StringSplitOptions.RemoveEmptyEntries).Length;
        var delay = TimeSpan.FromSeconds(words / WordsPerSecond) + _safetyMargin;

        if (delay < _minimumSafetyDelay)
        {
            delay = _minimumSafetyDelay;
        }
        else if (delay > _maximumSafetyDelay)
        {
            delay = _maximumSafetyDelay;
        }

        // Not awaited: the request that started the message has to be answered now, and the wait must not depend on it
        // living long enough to see it through. A scope taken from the shell host belongs to the tenant and outlives it.
        _ = HangUpLaterAsync(providerCallId, delay);
    }

    private async Task HangUpLaterAsync(string providerCallId, TimeSpan delay)
    {
        try
        {
            await Task.Delay(delay);

            var scope = await _shellHost.GetScopeAsync(_shellSettings);

            await scope.UsingAsync(async childScope =>
            {
                var telephonyService = childScope.ServiceProvider.GetService<ITelephonyService>();

                if (telephonyService is null)
                {
                    return;
                }

                var result = await telephonyService.HangupAsync(new CallReference
                {
                    CallId = providerCallId,
                }, CancellationToken.None);

                if (result?.Succeeded == true && _logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation(
                        "Hung up call {CallId} {Seconds:0} seconds after its abandoned-call message started, because the provider never reported the message finished.",
                        providerCallId.SanitizeLogValue(),
                        delay.TotalSeconds);
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "The safety hang-up after the abandoned-call message on call {CallId} failed.", providerCallId.SanitizeLogValue());
        }
    }

    private async Task<string> RenderMessageAsync(DialerProfile profile, Interaction interaction, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(profile.SafeHarborMessage))
        {
            return null;
        }

        var company = (await _siteService.GetSiteSettingsAsync())?.SiteName;

        // The number the person saw calling them is the one they can call back.
        var number = (await _callSessionManager.FindByInteractionIdAsync(interaction.ItemId, cancellationToken))?.FromAddress;

        if (string.IsNullOrWhiteSpace(number))
        {
            number = profile.CallerId;
        }

        return DialerAbandonment.Render(profile.SafeHarborMessage, company, number);
    }

    private async Task<bool> RecordAbandonedAsync(
        Interaction interaction,
        DialerProfile profile,
        string reason,
        DateTime occurredUtc,
        bool messagePlayed,
        CancellationToken cancellationToken)
    {
        // An abandoned call is a call a person answered. Every path that abandons one has heard the answer, but a path
        // that raced the answer's own record still counts it, so the rate's numerator never outgrows its denominator.
        var changed = DialerCallMetadata.GetLiveAnsweredUtc(interaction) is null &&
            await MarkLiveAnsweredAsync(interaction, profile, interaction.AnsweredUtc ?? occurredUtc, cancellationToken);

        if (!DialerCallMetadata.MarkAbandoned(interaction, reason))
        {
            return changed;
        }

        var data = ContactCenterCallAudit.ForInteraction(interaction);
        data.Reason = reason;
        data.Details["dialerProfileId"] = profile.ItemId;
        data.Details["messagePlayed"] = messagePlayed ? bool.TrueString : bool.FalseString;

        if (DialerCallMetadata.GetLiveAnsweredUtc(interaction) is { } answeredUtc && occurredUtc >= answeredUtc)
        {
            data.DurationSeconds = Math.Round((occurredUtc - answeredUtc).TotalSeconds, 3);
        }

        await PublishAsync(ContactCenterConstants.Events.DialerCallAbandoned, interaction, profile, occurredUtc, data, cancellationToken);

        return true;
    }

    private async Task<bool> MarkLiveAnsweredAsync(Interaction interaction, DialerProfile profile, DateTime answeredUtc, CancellationToken cancellationToken)
    {
        if (!DialerCallMetadata.MarkLiveAnswered(interaction, answeredUtc))
        {
            return false;
        }

        var data = ContactCenterCallAudit.ForInteraction(interaction);
        data.Details["dialerProfileId"] = profile.ItemId;

        if (DialerCallMetadata.GetAttemptNumber(interaction) is { } attemptNumber)
        {
            data.Details["attemptNumber"] = attemptNumber.ToString(CultureInfo.InvariantCulture);
        }

        await PublishAsync(ContactCenterConstants.Events.DialerLiveAnswered, interaction, profile, answeredUtc, data, cancellationToken);

        return true;
    }

    // Filed under the dialer profile, not the interaction: the rate is counted per profile from the aggregate index, one
    // seek per profile and event type, without reading the events themselves. Each fact is keyed on the call, so a
    // redelivered answer or a second path reporting the same abandonment records it once.
    private Task PublishAsync(
        string eventType,
        Interaction interaction,
        DialerProfile profile,
        DateTime occurredUtc,
        CallLifecycleEventData data,
        CancellationToken cancellationToken)
    {
        var interactionEvent = new InteractionEvent
        {
            EventType = eventType,
            InteractionId = interaction.ItemId,
            AggregateType = nameof(DialerProfile),
            AggregateId = profile.ItemId,
            OccurredUtc = DateTime.SpecifyKind(occurredUtc, DateTimeKind.Utc),
            ActorId = ContactCenterConstants.SystemActor,
            ActorType = ContactCenterActorType.System,
            SourceComponent = ContactCenterConstants.Components.Dialer,
            IdempotencyKey = $"dialer:{eventType}:{interaction.ItemId}",
        };

        interactionEvent.SetData(data);

        return _publisher.PublishAsync(interactionEvent, cancellationToken);
    }

    private async Task<DialerProfile> FindAutomatedProfileAsync(Interaction interaction, CancellationToken cancellationToken)
    {
        if (!DialerCallMetadata.IsCampaignDial(interaction))
        {
            return null;
        }

        var profileId = DialerCallMetadata.GetDialerProfileId(interaction);

        if (!_profiles.TryGetValue(profileId, out var profile))
        {
            profile = await _profileReader.FindByIdAsync(profileId, cancellationToken);
            _profiles[profileId] = profile;
        }

        return profile is not null && profile.Mode.IsAutomated() ? profile : null;
    }
}
