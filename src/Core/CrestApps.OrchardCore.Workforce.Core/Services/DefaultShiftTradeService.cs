using CrestApps.Core;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// The default <see cref="IShiftTradeService"/>.
/// </summary>
internal sealed class DefaultShiftTradeService : IShiftTradeService
{
    private readonly ShiftTradeStore _tradeStore;
    private readonly ICalendarEventManager _eventManager;
    private readonly WorkerStore _workerStore;
    private readonly IShiftService _shiftService;
    private readonly ShiftAssignmentContexts _contexts;
    private readonly TeamPlanningPolicy _policy;
    private readonly IWorkforceNotifier _notifier;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public DefaultShiftTradeService(
        ShiftTradeStore tradeStore,
        ICalendarEventManager eventManager,
        WorkerStore workerStore,
        IShiftService shiftService,
        ShiftAssignmentContexts contexts,
        TeamPlanningPolicy policy,
        IWorkforceNotifier notifier,
        IClock clock,
        IStringLocalizer<DefaultShiftTradeService> stringLocalizer)
    {
        _tradeStore = tradeStore;
        _eventManager = eventManager;
        _workerStore = workerStore;
        _shiftService = shiftService;
        _contexts = contexts;
        _policy = policy;
        _notifier = notifier;
        _clock = clock;
        S = stringLocalizer;
    }

    public async Task<PlanningResult<ShiftTrade>> OfferAsync(ShiftTradeOffer offer, string actor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(offer);

        var result = new PlanningResult<ShiftTrade>();
        var now = _clock.UtcNow;
        var shift = string.IsNullOrEmpty(offer.ShiftEventId) ? null : await _eventManager.FindByIdAsync(offer.ShiftEventId, cancellationToken);

        if (!ShiftEvents.TryRead(shift, out var data))
        {
            result.Fail(S["The shift doesn't exist."]);

            return result;
        }

        if (shift.StartUtc <= now)
        {
            result.Fail(S["The shift has already started."]);
        }

        if ((await _tradeStore.ListPendingForShiftAsync(shift.ItemId, cancellationToken)).Count > 0)
        {
            result.Fail(S["The shift is already in a trade."]);
        }

        var target = string.IsNullOrEmpty(offer.TargetWorkerId) ? null : await _workerStore.FindByIdAsync(offer.TargetWorkerId, cancellationToken);

        if (!string.IsNullOrEmpty(offer.TargetWorkerId) && target is null)
        {
            result.Fail(S["The colleague doesn't exist."]);
        }

        if (target?.ItemId == data.WorkerId)
        {
            result.Fail(S["Choose a colleague other than the worker offering the shift."]);
        }

        CalendarEvent targetShift = null;

        if (offer.Kind == ShiftTradeKind.Swap)
        {
            targetShift = string.IsNullOrEmpty(offer.TargetShiftEventId) ? null : await _eventManager.FindByIdAsync(offer.TargetShiftEventId, cancellationToken);

            if (target is null || !ShiftEvents.TryRead(targetShift, out var targetData) || targetData.WorkerId != target.ItemId)
            {
                result.Fail(S["A swap needs a colleague and one of their shifts."]);
            }
            else if (targetShift.StartUtc <= now)
            {
                result.Fail(S["The colleague's shift has already started."]);
            }
            else if ((await _tradeStore.ListPendingForShiftAsync(targetShift.ItemId, cancellationToken)).Count > 0)
            {
                result.Fail(S["The colleague's shift is already in a trade."]);
            }
        }

        if (!result.Succeeded)
        {
            return result;
        }

        var trade = new ShiftTrade
        {
            ItemId = UniqueId.GenerateId(),
            TeamId = data.TeamId,
            Kind = offer.Kind,
            Status = ShiftTradeStatus.Offered,
            OfferedByWorkerId = data.WorkerId,
            ShiftEventId = shift.ItemId,
            ShiftKey = data.ShiftKey ?? shift.SourceId,
            ShiftStartUtc = shift.StartUtc,
            ShiftEndUtc = shift.EndUtc,
            TargetWorkerId = target?.ItemId,
            TargetShiftEventId = targetShift?.ItemId,
            TargetShiftStartUtc = targetShift?.StartUtc,
            TargetShiftEndUtc = targetShift?.EndUtc,
            RequiresApproval = await _policy.TradesNeedApprovalAsync(data.TeamId, cancellationToken),
            Note = string.IsNullOrWhiteSpace(offer.Note) ? null : offer.Note.Trim(),
            CreatedBy = actor,
            CreatedUtc = now,
        };

        await _tradeStore.CreateAsync(trade, cancellationToken);
        result.Value = trade;

        if (!string.IsNullOrEmpty(trade.TargetWorkerId))
        {
            var from = await _workerStore.FindByIdAsync(trade.OfferedByWorkerId, cancellationToken);

            await NotifyAsync(trade.TargetWorkerId, trade.Kind == ShiftTradeKind.Swap ? S["{0} wants to swap shifts with you", from?.DisplayName] : S["{0} offers you a shift", from?.DisplayName], trade.Note, cancellationToken);
        }

        return result;
    }

    public async Task<ShiftTrade> FindAsync(string id, CancellationToken cancellationToken = default)
    {
        return string.IsNullOrEmpty(id) ? null : await _tradeStore.FindByIdAsync(id, cancellationToken);
    }

    public Task<(int Count, IReadOnlyList<ShiftTrade> Items)> PageAsync(ShiftTradeQuery query, int skip, int take, CancellationToken cancellationToken = default)
    {
        return _tradeStore.PageAsync(query ?? new ShiftTradeQuery(), skip, take, cancellationToken);
    }

    public async Task<IReadOnlyList<string>> CheckAsync(string id, string workerId, CancellationToken cancellationToken = default)
    {
        var trade = await FindAsync(id, cancellationToken);

        if (trade is null)
        {
            return [S["The trade doesn't exist."]];
        }

        return await CheckAsync(trade, workerId, ShiftTradeStatus.Offered, cancellationToken);
    }

    public async Task<PlanningResult<ShiftTrade>> AcceptAsync(string id, string workerId, string actor, CancellationToken cancellationToken = default)
    {
        var result = new PlanningResult<ShiftTrade>();
        var trade = await FindAsync(id, cancellationToken);

        if (trade is null)
        {
            result.Fail(S["The trade doesn't exist."]);

            return result;
        }

        foreach (var problem in await CheckAsync(trade, workerId, ShiftTradeStatus.Offered, cancellationToken))
        {
            result.Fail(problem);
        }

        if (!result.Succeeded)
        {
            return result;
        }

        trade.AcceptedByWorkerId = workerId;
        trade.AcceptedUtc = _clock.UtcNow;

        if (trade.RequiresApproval)
        {
            trade.Status = ShiftTradeStatus.Accepted;
            await _tradeStore.UpdateAsync(trade, cancellationToken);
            result.Value = trade;

            await NotifyAsync(trade.OfferedByWorkerId, S["Your trade was accepted"], S["It waits for a manager to approve it."], cancellationToken);

            return result;
        }

        await ApplyAsync(trade, actor, result, cancellationToken);

        return result;
    }

    public async Task<PlanningResult<ShiftTrade>> RejectAsync(string id, string workerId, string actor, CancellationToken cancellationToken = default)
    {
        var result = new PlanningResult<ShiftTrade>();
        var trade = await FindAsync(id, cancellationToken);

        if (trade is null || trade.Status != ShiftTradeStatus.Offered || trade.TargetWorkerId is null || trade.TargetWorkerId != workerId)
        {
            result.Fail(S["Only the colleague a trade is offered to can turn it down."]);

            return result;
        }

        await EndAsync(trade, ShiftTradeStatus.Rejected, null, actor, cancellationToken);
        result.Value = trade;

        await NotifyAsync(trade.OfferedByWorkerId, S["Your trade was turned down"], null, cancellationToken);

        return result;
    }

    public async Task<PlanningResult<ShiftTrade>> ApproveAsync(string id, string actor, CancellationToken cancellationToken = default)
    {
        var result = new PlanningResult<ShiftTrade>();
        var trade = await FindAsync(id, cancellationToken);

        if (trade is null || trade.Status != ShiftTradeStatus.Accepted)
        {
            result.Fail(S["Only an accepted trade can be approved."]);

            return result;
        }

        foreach (var problem in await CheckAsync(trade, trade.AcceptedByWorkerId, ShiftTradeStatus.Accepted, cancellationToken))
        {
            result.Fail(problem);
        }

        if (!result.Succeeded)
        {
            return result;
        }

        await ApplyAsync(trade, actor, result, cancellationToken);

        return result;
    }

    public async Task<PlanningResult<ShiftTrade>> DeclineAsync(string id, string note, string actor, CancellationToken cancellationToken = default)
    {
        var result = new PlanningResult<ShiftTrade>();
        var trade = await FindAsync(id, cancellationToken);

        if (trade is null || trade.Status is not (ShiftTradeStatus.Offered or ShiftTradeStatus.Accepted))
        {
            result.Fail(S["The trade is no longer open."]);

            return result;
        }

        await EndAsync(trade, ShiftTradeStatus.Declined, note, actor, cancellationToken);
        result.Value = trade;

        await NotifyAsync(trade.OfferedByWorkerId, S["Your trade was declined"], note, cancellationToken);

        return result;
    }

    public async Task<PlanningResult<ShiftTrade>> CancelAsync(string id, string actor, CancellationToken cancellationToken = default)
    {
        var result = new PlanningResult<ShiftTrade>();
        var trade = await FindAsync(id, cancellationToken);

        if (trade is null || trade.Status is not (ShiftTradeStatus.Offered or ShiftTradeStatus.Accepted))
        {
            result.Fail(S["The trade is no longer open."]);

            return result;
        }

        await EndAsync(trade, ShiftTradeStatus.Canceled, null, actor, cancellationToken);
        result.Value = trade;

        return result;
    }

    /// <summary>
    /// Returns why a worker can't accept or be approved for a trade: the trade's state, who may accept, the shifts
    /// still being as offered, and the rules for each side.
    /// </summary>
    private async Task<IReadOnlyList<string>> CheckAsync(ShiftTrade trade, string workerId, ShiftTradeStatus expected, CancellationToken cancellationToken)
    {
        if (trade.Status != expected)
        {
            return [S["The trade is no longer open."]];
        }

        var worker = string.IsNullOrEmpty(workerId) ? null : await _workerStore.FindByIdAsync(workerId, cancellationToken);

        if (worker is null)
        {
            return [S["The worker doesn't exist."]];
        }

        if (worker.ItemId == trade.OfferedByWorkerId)
        {
            return [S["A worker can't accept their own trade."]];
        }

        if (trade.TargetWorkerId is not null && trade.TargetWorkerId != worker.ItemId)
        {
            return [S["The trade is offered to someone else."]];
        }

        var shift = await _eventManager.FindByIdAsync(trade.ShiftEventId, cancellationToken);

        if (!ShiftEvents.TryRead(shift, out var data) || data.WorkerId != trade.OfferedByWorkerId || shift.StartUtc != trade.ShiftStartUtc || shift.EndUtc != trade.ShiftEndUtc)
        {
            return [S["The shift changed after it was offered."]];
        }

        if (trade.Kind == ShiftTradeKind.GiveAway)
        {
            return await _shiftService.CheckAsync(await _contexts.ForEventAsync(shift, data, worker, [data.ShiftKey, shift.SourceId], cancellationToken), cancellationToken);
        }

        var targetShift = await _eventManager.FindByIdAsync(trade.TargetShiftEventId, cancellationToken);

        if (!ShiftEvents.TryRead(targetShift, out var targetData) || targetData.WorkerId != worker.ItemId || targetShift.StartUtc != trade.TargetShiftStartUtc || targetShift.EndUtc != trade.TargetShiftEndUtc)
        {
            return [S["The colleague's shift changed after the trade was offered."]];
        }

        var offeredBy = await _workerStore.FindByIdAsync(trade.OfferedByWorkerId, cancellationToken);
        string[] keys = [data.ShiftKey, shift.SourceId, targetData.ShiftKey, targetShift.SourceId];

        return (await _shiftService.CheckAsync(await _contexts.ForEventAsync(shift, data, worker, keys, cancellationToken), cancellationToken))
            .Concat(await _shiftService.CheckAsync(await _contexts.ForEventAsync(targetShift, targetData, offeredBy, keys, cancellationToken), cancellationToken))
            .ToArray();
    }

    private async Task ApplyAsync(ShiftTrade trade, string actor, PlanningResult<ShiftTrade> result, CancellationToken cancellationToken)
    {
        var reason = trade.Kind == ShiftTradeKind.Swap ? S["Shift swap"].Value : S["Shift given away"].Value;
        IReadOnlyList<string> errors;

        if (trade.Kind == ShiftTradeKind.Swap)
        {
            errors = (await _shiftService.SwapAsync(trade.ShiftEventId, trade.TargetShiftEventId, ScheduleChangeInitiator.Worker, reason, actor, cancellationToken)).Errors;
        }
        else
        {
            errors = (await _shiftService.ReassignAsync(trade.ShiftEventId, trade.AcceptedByWorkerId, ScheduleChangeInitiator.Worker, reason, actor, cancellationToken)).Errors;
        }

        foreach (var error in errors)
        {
            result.Fail(error);
        }

        if (!result.Succeeded)
        {
            return;
        }

        trade.Status = ShiftTradeStatus.Approved;
        trade.DecidedBy = actor;
        trade.DecidedUtc = _clock.UtcNow;
        await _tradeStore.UpdateAsync(trade, cancellationToken);

        // The shifts changed hands, so the other offers of them no longer stand.
        foreach (var eventId in new[] { trade.ShiftEventId, trade.TargetShiftEventId }.Where(id => !string.IsNullOrEmpty(id)))
        {
            foreach (var other in await _tradeStore.ListPendingForShiftAsync(eventId, cancellationToken))
            {
                if (other.ItemId != trade.ItemId)
                {
                    await EndAsync(other, ShiftTradeStatus.Canceled, S["The shift was traded in another offer."], actor, cancellationToken);
                }
            }
        }

        result.Value = trade;
    }

    private Task NotifyAsync(string workerId, string title, string body, CancellationToken cancellationToken)
    {
        return string.IsNullOrEmpty(workerId)
            ? Task.CompletedTask
            : _notifier.NotifyAsync(new WorkforceNotification
            {
                WorkerId = workerId,
                Kind = WorkforceNotificationKind.ShiftTrade,
                Title = title,
                Body = body ?? string.Empty,
                Url = "my-work/shifts",
            }, cancellationToken);
    }

    private async Task EndAsync(ShiftTrade trade, ShiftTradeStatus status, string note, string actor, CancellationToken cancellationToken)
    {
        trade.Status = status;
        trade.DecisionNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        trade.DecidedBy = actor;
        trade.DecidedUtc = _clock.UtcNow;
        await _tradeStore.UpdateAsync(trade, cancellationToken);
    }
}
