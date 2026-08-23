using CrestApps.Core.Support;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Indexes;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using YesSql;

namespace CrestApps.OrchardCore.Dialpad.Services;

/// <summary>
/// Routes direct inbound Dialpad call events to the connected Orchard user that owns the target Dialpad account.
/// </summary>
public sealed class DialpadDirectInboundCallRouter : IDialpadInboundCallRouter
{
    private readonly ISession _session;
    private readonly ILookupNormalizer _lookupNormalizer;
    private readonly IIncomingCallDispatcher _incomingCallDispatcher;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialpadDirectInboundCallRouter"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="lookupNormalizer">The lookup normalizer used for email matching.</param>
    /// <param name="incomingCallDispatcher">The dispatcher that notifies the resolved user.</param>
    /// <param name="logger">The logger.</param>
    public DialpadDirectInboundCallRouter(
        ISession session,
        ILookupNormalizer lookupNormalizer,
        IIncomingCallDispatcher incomingCallDispatcher,
        ILogger<DialpadDirectInboundCallRouter> logger)
    {
        _session = session;
        _lookupNormalizer = lookupNormalizer;
        _incomingCallDispatcher = incomingCallDispatcher;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<bool> RouteAsync(
        DialpadCallEvent callEvent,
        DateTime occurredUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(callEvent);

        if (!string.IsNullOrWhiteSpace(callEvent.TargetType) &&
            !string.Equals(callEvent.TargetType, "user", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var userId = await ResolveUserIdAsync(callEvent, cancellationToken);

        if (string.IsNullOrEmpty(userId))
        {
            return false;
        }

        var call = new TelephonyCall
        {
            CallId = callEvent.CallId,
            From = DialpadCallEventAddressResolver.ResolveFromAddress(callEvent),
            To = DialpadCallEventAddressResolver.ResolveToAddress(callEvent),
            State = MapCallState(callEvent.State),
            Direction = CallDirection.Inbound,
            ProviderName = DialpadConstants.ProviderTechnicalName,
            StartedUtc = new DateTimeOffset(DateTime.SpecifyKind(occurredUtc, DateTimeKind.Utc)),
        };

        if (!string.IsNullOrWhiteSpace(callEvent.ContactName))
        {
            call.Metadata["dialpadContactName"] = callEvent.ContactName;
        }

        if (!string.IsNullOrWhiteSpace(callEvent.TargetName))
        {
            call.Metadata["dialpadTargetName"] = callEvent.TargetName;
        }

        await _incomingCallDispatcher.DispatchAsync(userId, call, cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Routed Dialpad inbound call {CallId} to soft-phone user {UserId} using the connected Dialpad account mapping.",
                callEvent.CallId.SanitizeLogValue(),
                userId.SanitizeLogValue());
        }

        return true;
    }

    private async Task<string> ResolveUserIdAsync(DialpadCallEvent callEvent, CancellationToken cancellationToken)
    {
        var userId = await ResolveUniqueUserIdAsync(
            callEvent.TargetId,
            value => _session.QueryIndex<TelephonyUserConnectionIndex>(index =>
                index.ProviderName == DialpadConstants.ProviderTechnicalName &&
                index.IsEnabled &&
                index.RemoteUserId == value),
            "target id",
            callEvent,
            cancellationToken);

        if (!string.IsNullOrEmpty(userId))
        {
            return userId;
        }

        var normalizedEmail = NormalizeEmail(callEvent.TargetEmail);
        userId = await ResolveUniqueUserIdAsync(
            normalizedEmail,
            value => _session.QueryIndex<TelephonyUserConnectionIndex>(index =>
                index.ProviderName == DialpadConstants.ProviderTechnicalName &&
                index.IsEnabled &&
                index.NormalizedRemoteUserEmail == value),
            "target email",
            callEvent,
            cancellationToken);

        if (!string.IsNullOrEmpty(userId))
        {
            return userId;
        }

        var normalizedPhone = DialpadAddressNormalizer.NormalizePhoneNumber(callEvent.TargetPhone);
        userId = await ResolveUniqueUserIdAsync(
            normalizedPhone,
            value => _session.QueryIndex<TelephonyUserConnectionIndex>(index =>
                index.ProviderName == DialpadConstants.ProviderTechnicalName &&
                index.IsEnabled &&
                index.NormalizedRemotePhoneNumber == value),
            "target phone",
            callEvent,
            cancellationToken);

        if (!string.IsNullOrEmpty(userId))
        {
            return userId;
        }

        var serviceAddress = DialpadAddressNormalizer.NormalizePhoneNumber(
            DialpadCallEventAddressResolver.ResolveServiceAddress(callEvent));

        return await ResolveUniqueUserIdAsync(
            serviceAddress,
            value => _session.QueryIndex<TelephonyUserConnectionIndex>(index =>
                index.ProviderName == DialpadConstants.ProviderTechnicalName &&
                index.IsEnabled &&
                index.NormalizedRemotePhoneNumber == value),
            "service address",
            callEvent,
            cancellationToken);
    }

    private async Task<string> ResolveUniqueUserIdAsync(
        string candidateValue,
        Func<string, IQueryIndex<TelephonyUserConnectionIndex>> queryFactory,
        string candidateSource,
        DialpadCallEvent callEvent,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(candidateValue))
        {
            return null;
        }

        var matches = (await queryFactory(candidateValue).ListAsync(cancellationToken))
            .Select(index => index.UserId)
            .Where(userId => !string.IsNullOrWhiteSpace(userId))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (matches.Count == 1)
        {
            return matches[0];
        }

        if (matches.Count > 1)
        {
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning(
                    "Dialpad inbound call {CallId} matched multiple connected users for {CandidateSource}. The event was not routed.",
                    callEvent.CallId.SanitizeLogValue(),
                    candidateSource);
            }
        }

        return null;
    }

    private string NormalizeEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        return _lookupNormalizer.NormalizeEmail(email.Trim());
    }

    private static CallState MapCallState(string state)
    {
        return state?.Trim().ToLowerInvariant() switch
        {
            "calling" or "dialing" or "connecting" or "preanswer" => CallState.Connecting,
            "connected" or "active" or "human" or "live" => CallState.Connected,
            _ => CallState.Ringing,
        };
    }
}
