using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.Extensions.Logging;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// The part of inbound routing that decides the priority a caller is queued at: the entry point's priority, raised
/// by whatever the caller-based priority contributors notice about this caller.
/// </summary>
public sealed partial class InboundVoiceCallProcessor
{
    /// <summary>
    /// How long after we set up a callback to a number a call from that number counts as the customer returning it.
    /// A customer who missed our call usually rings back the same or the next day.
    /// </summary>
    internal static readonly TimeSpan ReturningCallbackWindow = TimeSpan.FromHours(24);

    /// <summary>
    /// Resolves the priority the call is enqueued at. Returns the entry point's own priority (or <see langword="null"/>
    /// for the queue default, as before) unless a contributor raised the caller above it.
    /// </summary>
    private async Task<InteractionPriority?> ResolveQueuePriorityAsync(
        EntryPointRoutingPlan plan,
        ActivityQueue queue,
        string queueId,
        OmnichannelActivity activity,
        Core.Models.Interaction interaction,
        string fromAddress,
        string serviceAddress,
        DateTime now,
        CancellationToken cancellationToken)
    {
        // The entry point's priority is the operator's decision about this number; without an entry point the
        // queue's default applies. Contributors may raise it, never lower it.
        var configuredPriority = plan is not null ? plan.Priority : (InteractionPriority?)null;
        var baseline = configuredPriority ?? queue?.DefaultPriority ?? InteractionPriority.Normal;

        var context = new InboundPriorityContext
        {
            ConfiguredPriority = baseline,
            CustomerAddress = fromAddress,
            ServiceAddress = serviceAddress,
            QueueId = queueId,
            ContactContentItemId = activity.ContactContentItemId,
            CampaignId = activity.CampaignId,
        };

        // The contributors judge the caller from what we already know about them, so it is looked up here, once.
        if (!string.IsNullOrEmpty(fromAddress))
        {
            try
            {
                context.LastInboundUtc = await FindLastInboundCallUtcAsync(fromAddress, activity.ItemId, cancellationToken);
                context.IsReturningCallback = await HasRecentCallbackAsync(fromAddress, now, cancellationToken);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                // A caller must not be dropped because their history could not be read; they queue at the
                // configured priority instead, as the resolver does when a contributor fails.
                _logger.LogWarning(
                    ex,
                    "The caller history for interaction '{InteractionId}' could not be read, so caller-based priority is judged without it.",
                    interaction.ItemId.SanitizeLogValue());
            }
        }

        var resolved = await _priorityResolver.ResolveAsync(context, cancellationToken);

        if (resolved <= baseline)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Interaction '{InteractionId}' is queued at the configured priority {Priority} (repeat caller: {IsRepeatCaller}, returning callback: {IsReturningCallback}).",
                    interaction.ItemId.SanitizeLogValue(),
                    baseline.ToString().SanitizeLogValue(),
                    context.LastInboundUtc.HasValue,
                    context.IsReturningCallback);
            }

            // Unchanged, so the queue default still applies exactly as it did when no entry point named a priority.
            return configuredPriority;
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Interaction '{InteractionId}' is queued at priority {Priority}, raised from the configured {ConfiguredPriority} by the caller's history (repeat caller: {IsRepeatCaller}, returning callback: {IsReturningCallback}).",
                interaction.ItemId.SanitizeLogValue(),
                resolved.ToString().SanitizeLogValue(),
                baseline.ToString().SanitizeLogValue(),
                context.LastInboundUtc.HasValue,
                context.IsReturningCallback);
        }

        return resolved;
    }

    // The caller's previous inbound call, if any. The call being routed now is excluded, as it may already be written.
    private async Task<DateTime?> FindLastInboundCallUtcAsync(string fromAddress, string currentActivityItemId, CancellationToken cancellationToken)
    {
        var previous = await _session.QueryIndex<OmnichannelActivityIndex>(
                index => index.Source == ActivitySources.Inbound &&
                    index.Kind == ActivityKind.Call &&
                    index.PreferredDestination == fromAddress &&
                    index.ItemId != currentActivityItemId,
                collection: OmnichannelConstants.CollectionName)
            .OrderByDescending(index => index.CreatedUtc)
            .FirstOrDefaultAsync(cancellationToken);

        return previous?.CreatedUtc;
    }

    // A callback to this number that became outbound work recently: the caller was promised a call and is ringing us.
    // A callback still waiting for its time has no activity yet, so it is not seen here.
    private async Task<bool> HasRecentCallbackAsync(string fromAddress, DateTime now, CancellationToken cancellationToken)
    {
        var since = now - ReturningCallbackWindow;

        var callback = await _session.QueryIndex<OmnichannelActivityIndex>(
                index => index.Source == ActivitySources.Callback &&
                    index.PreferredDestination == fromAddress &&
                    index.CreatedUtc >= since,
                collection: OmnichannelConstants.CollectionName)
            .FirstOrDefaultAsync(cancellationToken);

        return callback is not null;
    }
}
