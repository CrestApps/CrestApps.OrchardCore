using System.Security.Claims;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="ISecureCaptureService"/>. It is the ownership-checked,
/// policy-gated boundary that starts a secure capture, resolves and completes it for the customer, and settles it
/// on cancellation or expiry, ensuring the raw sensitive value only ever reaches the tokenization sink.
/// </summary>
public sealed class SecureCaptureService : ISecureCaptureService
{
    private const int ExpiryBatchLimit = 200;

    private readonly IInteractionManager _interactionManager;
    private readonly ISecureCaptureSessionManager _sessionManager;
    private readonly ICallControlAuthorizationService _callControlAuthorizationService;
    private readonly ISecureCaptureTokenSink _tokenSink;
    private readonly IContactCenterRecordingService _recordingService;
    private readonly IContactCenterEventPublisher _publisher;
    private readonly ISiteService _siteService;
    private readonly IClock _clock;
    private readonly ILogger<SecureCaptureService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SecureCaptureService"/> class.
    /// </summary>
    /// <param name="interactionManager">The interaction manager used to resolve and own-check the interaction.</param>
    /// <param name="sessionManager">The secure capture session manager.</param>
    /// <param name="callControlAuthorizationService">The shared call-control authorization boundary.</param>
    /// <param name="tokenSink">The tokenization sink the raw value is exchanged through.</param>
    /// <param name="recordingService">The recording service used to resume recording a capture had paused.</param>
    /// <param name="publisher">The event publisher used to record the capture lifecycle in the audit history.</param>
    /// <param name="siteService">The site service used to read the tenant secure capture settings.</param>
    /// <param name="clock">The clock used to stamp capture times.</param>
    /// <param name="logger">The logger instance.</param>
    public SecureCaptureService(
        IInteractionManager interactionManager,
        ISecureCaptureSessionManager sessionManager,
        ICallControlAuthorizationService callControlAuthorizationService,
        ISecureCaptureTokenSink tokenSink,
        IContactCenterRecordingService recordingService,
        IContactCenterEventPublisher publisher,
        ISiteService siteService,
        IClock clock,
        ILogger<SecureCaptureService> logger)
    {
        _interactionManager = interactionManager;
        _sessionManager = sessionManager;
        _callControlAuthorizationService = callControlAuthorizationService;
        _tokenSink = tokenSink;
        _recordingService = recordingService;
        _publisher = publisher;
        _siteService = siteService;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<SecureCaptureBeginResult> BeginAsync(
        string interactionId,
        string userId,
        ClaimsPrincipal principal,
        IReadOnlyCollection<SecureCaptureField> fields,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(interactionId) || string.IsNullOrEmpty(userId))
        {
            return SecureCaptureBeginResult.Failure("An interaction and an agent are required.");
        }

        if (fields is null || fields.Count == 0)
        {
            return SecureCaptureBeginResult.Failure("At least one field to capture is required.");
        }

        var distinctFields = fields.Distinct().ToArray();

        if (Array.Exists(distinctFields, field => !Enum.IsDefined(field)))
        {
            return SecureCaptureBeginResult.Failure("One or more requested fields are not recognized.");
        }

        var settings = await GetSettingsAsync();

        if (!settings.Enabled)
        {
            return SecureCaptureBeginResult.Failure("Secure data capture is not enabled for this tenant.");
        }

        var interaction = await _interactionManager.FindByIdAsync(interactionId, cancellationToken);

        if (interaction is null)
        {
            return SecureCaptureBeginResult.Failure("The interaction could not be found.");
        }

        var authorization = await AuthorizeAsync(interaction, userId, principal, cancellationToken);

        if (!authorization.Succeeded)
        {
            return SecureCaptureBeginResult.Failure(authorization.FailureReason);
        }

        // Only one capture may be in progress for an interaction at a time: a second concurrent capture would let
        // one capture's completion or expiry resume recording while the other is still collecting, and would
        // multiply the recording-pause bookkeeping. Refuse to start a second while one is still collecting.
        var active = await _sessionManager.FindActiveByInteractionAsync(interaction.ItemId, cancellationToken);

        if (active is not null)
        {
            return SecureCaptureBeginResult.Failure("A secure capture is already in progress for this interaction.");
        }

        var (rawToken, tokenHash) = SecureCaptureAccessToken.Create();
        var now = _clock.UtcNow;
        var ttlSeconds = Math.Clamp(
            settings.LinkTimeToLiveSeconds,
            SecureCaptureSettings.MinLinkTimeToLiveSeconds,
            SecureCaptureSettings.MaxLinkTimeToLiveSeconds);
        var expiresUtc = now.AddSeconds(ttlSeconds);

        var session = await _sessionManager.NewAsync(cancellationToken: cancellationToken);
        session.InteractionId = interaction.ItemId;
        session.AgentId = authorization.AgentId ?? userId;
        session.RequestedFields = distinctFields;
        session.State = SecureCaptureState.Collecting;
        session.AccessTokenHash = tokenHash;
        session.CreatedUtc = now;
        session.ExpiresUtc = expiresUtc;
        session.ModifiedUtc = now;

        // Pausing recording is defense in depth: the customer already enters the data on a separate page that
        // never touches the agent voice channel, but a provider that records the whole media path should still
        // not retain the sensitive segment. Attempt it, record whether it engaged so the completion path can
        // resume, and never fail the capture if the provider cannot pause.
        if (settings.PauseRecordingDuringCapture)
        {
            var pause = await _recordingService.PauseAsync(interaction.ItemId, cancellationToken);
            session.EngagedRecordingPause = pause.Succeeded;
        }

        try
        {
            await _sessionManager.CreateAsync(session, cancellationToken);
        }
        catch
        {
            // The capture could not be persisted after recording was paused. Resume immediately so a failed start
            // never leaves recording suppressed with no session for the safety net to settle.
            await ResumeRecordingIfEngagedAsync(session, cancellationToken);

            throw;
        }

        await PublishAsync(
            ContactCenterConstants.Events.SecureCaptureStarted,
            interaction.ItemId,
            session.AgentId,
            new Dictionary<string, string>
            {
                ["sessionId"] = session.ItemId,
                ["fields"] = string.Join(",", distinctFields),
                ["recordingPaused"] = session.EngagedRecordingPause ? "true" : "false",
            });

        return SecureCaptureBeginResult.Success(session.ItemId, rawToken, expiresUtc);
    }

    /// <inheritdoc/>
    public async Task<SecureCaptureSession> GetForCustomerAsync(string rawAccessToken, CancellationToken cancellationToken = default)
    {
        var hash = SecureCaptureAccessToken.Hash(rawAccessToken);

        if (hash is null)
        {
            return null;
        }

        var session = await _sessionManager.FindByAccessTokenHashAsync(hash, cancellationToken);

        if (session is null || session.State != SecureCaptureState.Collecting || session.ExpiresUtc <= _clock.UtcNow)
        {
            return null;
        }

        return session;
    }

    /// <inheritdoc/>
    public async Task<SecureCaptureActionResult> SubmitAsync(
        string rawAccessToken,
        IReadOnlyDictionary<SecureCaptureField, string> values,
        CancellationToken cancellationToken = default)
    {
        if (values is null || values.Count == 0)
        {
            return SecureCaptureActionResult.Failure("No values were submitted.");
        }

        var hash = SecureCaptureAccessToken.Hash(rawAccessToken);

        if (hash is null)
        {
            return SecureCaptureActionResult.Failure("The secure link is not valid.");
        }

        var session = await _sessionManager.FindByAccessTokenHashAsync(hash, cancellationToken);

        if (session is null || session.State != SecureCaptureState.Collecting)
        {
            return SecureCaptureActionResult.Failure("The secure link is no longer available.");
        }

        if (session.ExpiresUtc <= _clock.UtcNow)
        {
            return SecureCaptureActionResult.Failure("The secure link has expired.");
        }

        var maskedValues = new Dictionary<SecureCaptureField, string>();
        var tokenReferences = new Dictionary<SecureCaptureField, string>();

        // Tokenize every requested field before persisting anything, so a single invalid value cannot leave the
        // capture half-completed with some raw values already exchanged and others rejected.
        foreach (var field in session.RequestedFields)
        {
            if (!values.TryGetValue(field, out var rawValue) || string.IsNullOrWhiteSpace(rawValue))
            {
                return SecureCaptureActionResult.Failure("All requested values are required.");
            }

            // A stable per-capture, per-field idempotency key lets a production sink de-duplicate a retried or
            // replayed submission: if the customer double-submits, or the scope commit fails after the sink was
            // called and the customer resubmits, the sink exchanges the value once instead of minting a second
            // vault token.
            var idempotencyKey = $"{session.ItemId}:{field}";
            var tokenResult = await _tokenSink.TokenizeAsync(field, rawValue, idempotencyKey, cancellationToken);

            if (!tokenResult.Succeeded)
            {
                return SecureCaptureActionResult.Failure(tokenResult.ErrorMessage ?? "The submitted value is not valid.");
            }

            // A non-retainable value, such as a card security code, is validated and used for its one-shot
            // purpose but must never be stored in any form, so neither its token reference nor a masked value is
            // kept on the session.
            if (!tokenResult.IsRetainable)
            {
                continue;
            }

            maskedValues[field] = tokenResult.MaskedValue;
            tokenReferences[field] = tokenResult.Token;
        }

        var now = _clock.UtcNow;
        session.MaskedValues = maskedValues;
        session.TokenReferences = tokenReferences;
        session.State = SecureCaptureState.Completed;
        session.CompletedUtc = now;
        session.ModifiedUtc = now;

        await ResumeRecordingIfEngagedAsync(session, cancellationToken);

        await _sessionManager.UpdateAsync(session, cancellationToken: cancellationToken);

        await PublishAsync(
            ContactCenterConstants.Events.SecureCaptureCompleted,
            session.InteractionId,
            session.AgentId,
            BuildCompletionData(session));

        return SecureCaptureActionResult.Success();
    }

    /// <inheritdoc/>
    public async Task<SecureCaptureActionResult> CancelAsync(
        string sessionId,
        string userId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(sessionId) || string.IsNullOrEmpty(userId))
        {
            return SecureCaptureActionResult.Failure("A capture and an agent are required.");
        }

        var session = await _sessionManager.FindByIdAsync(sessionId, cancellationToken);

        if (session is null || session.State != SecureCaptureState.Collecting)
        {
            return SecureCaptureActionResult.Failure("The capture is no longer active.");
        }

        var interaction = await _interactionManager.FindByIdAsync(session.InteractionId, cancellationToken);

        if (interaction is null)
        {
            return SecureCaptureActionResult.Failure("The interaction could not be found.");
        }

        var authorization = await AuthorizeAsync(interaction, userId, principal, cancellationToken);

        if (!authorization.Succeeded)
        {
            return SecureCaptureActionResult.Failure(authorization.FailureReason);
        }

        var now = _clock.UtcNow;
        session.State = SecureCaptureState.Cancelled;
        session.CancelledUtc = now;
        session.ModifiedUtc = now;

        await ResumeRecordingIfEngagedAsync(session, cancellationToken);

        await _sessionManager.UpdateAsync(session, cancellationToken: cancellationToken);

        await PublishAsync(
            ContactCenterConstants.Events.SecureCaptureCancelled,
            session.InteractionId,
            authorization.AgentId ?? userId,
            new Dictionary<string, string>
            {
                ["sessionId"] = session.ItemId,
            });

        return SecureCaptureActionResult.Success();
    }

    /// <inheritdoc/>
    public async Task<int> ExpireDueAsync(int maxCount, CancellationToken cancellationToken = default)
    {
        var take = maxCount <= 0 ? ExpiryBatchLimit : Math.Min(maxCount, ExpiryBatchLimit);
        var sessions = await _sessionManager.GetExpiredAsync(_clock.UtcNow, take, cancellationToken);

        var expired = 0;

        foreach (var session in sessions)
        {
            // Re-check the state under the current transaction: a concurrent submit or cancel may have already
            // settled the capture since the query snapshot was taken.
            if (session.State != SecureCaptureState.Collecting)
            {
                continue;
            }

            var now = _clock.UtcNow;
            session.State = SecureCaptureState.Expired;
            session.ModifiedUtc = now;

            await ResumeRecordingIfEngagedAsync(session, cancellationToken);

            await _sessionManager.UpdateAsync(session, cancellationToken: cancellationToken);

            await PublishAsync(
                ContactCenterConstants.Events.SecureCaptureCancelled,
                session.InteractionId,
                session.AgentId,
                new Dictionary<string, string>
                {
                    ["sessionId"] = session.ItemId,
                    ["reason"] = "expired",
                });

            expired++;
        }

        return expired;
    }

    /// <inheritdoc/>
    public async Task<int> RecoverRecordingResumesAsync(int maxCount, CancellationToken cancellationToken = default)
    {
        var take = maxCount <= 0 ? ExpiryBatchLimit : Math.Min(maxCount, ExpiryBatchLimit);
        var sessions = await _sessionManager.GetPendingRecordingResumeAsync(take, cancellationToken);

        var resumed = 0;

        foreach (var session in sessions)
        {
            // Re-check under the current transaction: another pass may have already resumed and settled this one.
            if (!session.EngagedRecordingPause || session.RecordingResumed)
            {
                continue;
            }

            await ResumeRecordingIfEngagedAsync(session, cancellationToken);

            if (!session.RecordingResumed)
            {
                continue;
            }

            await _sessionManager.UpdateAsync(session, cancellationToken: cancellationToken);

            resumed++;
        }

        return resumed;
    }

    private async Task ResumeRecordingIfEngagedAsync(SecureCaptureSession session, CancellationToken cancellationToken)
    {
        if (!session.EngagedRecordingPause || session.RecordingResumed || string.IsNullOrEmpty(session.InteractionId))
        {
            return;
        }

        try
        {
            var result = await _recordingService.ResumeAsync(session.InteractionId, cancellationToken);

            if (result is not null && result.Succeeded)
            {
                session.RecordingResumed = true;

                return;
            }

            _logger.LogWarning(
                "Failed to resume recording for interaction {InteractionId} after secure capture {SessionId} settled: {Reason}. It will be retried by the recovery pass.",
                session.InteractionId.SanitizeLogValue(),
                session.ItemId.SanitizeLogValue(),
                result?.Reason);
        }
        catch (Exception ex)
        {
            // A resume failure must never surface from a settlement path and undo the terminal transition. The
            // session stays flagged as not yet resumed so the recovery pass retries it.
            _logger.LogWarning(
                ex,
                "Resuming recording for interaction {InteractionId} after secure capture {SessionId} settled threw. It will be retried by the recovery pass.",
                session.InteractionId.SanitizeLogValue(),
                session.ItemId.SanitizeLogValue());
        }
    }

    private Task<CallControlAuthorizationResult> AuthorizeAsync(
        Interaction interaction,
        string userId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        return _callControlAuthorizationService.AuthorizeAsync(new CallControlAuthorizationContext
        {
            Principal = principal,
            UserId = userId,
            Verb = CallControlVerb.RecordingControl,
            InteractionId = interaction.ItemId,
            ProviderName = interaction.ProviderName,
            ProviderCallId = interaction.ProviderInteractionId,
        }, cancellationToken);
    }

    private async Task<SecureCaptureSettings> GetSettingsAsync()
    {
        var site = await _siteService.GetSiteSettingsAsync();

        return site.GetOrCreate<SecureCaptureSettings>();
    }

    private async Task PublishAsync(
        string eventType,
        string interactionId,
        string actorId,
        Dictionary<string, string> data)
    {
        if (string.IsNullOrEmpty(interactionId))
        {
            return;
        }

        var interactionEvent = new InteractionEvent
        {
            EventType = eventType,
            InteractionId = interactionId,
            AggregateType = nameof(Interaction),
            AggregateId = interactionId,
            ActorId = actorId,
            SourceComponent = ContactCenterConstants.Components.SecureCapture,
        };

        interactionEvent.SetData(data);

        await _publisher.PublishAsync(interactionEvent, CancellationToken.None);
    }

    private static Dictionary<string, string> BuildCompletionData(SecureCaptureSession session)
    {
        var data = new Dictionary<string, string>
        {
            ["sessionId"] = session.ItemId,
            ["fields"] = string.Join(",", session.RequestedFields),
        };

        // Only the masked representation is ever placed on the audit event; the raw value never leaves the token
        // sink and the token reference is not disclosed to the event history.
        foreach (var masked in session.MaskedValues)
        {
            data[$"masked:{masked.Key}"] = masked.Value;
        }

        return data;
    }
}
