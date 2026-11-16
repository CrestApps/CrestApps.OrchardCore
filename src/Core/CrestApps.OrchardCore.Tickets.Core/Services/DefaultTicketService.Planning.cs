using System.Globalization;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Tickets.Services;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Timeline.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tickets.Core.Services;

/// <summary>
/// A ticket's planning values: impact and urgency (which set the priority through the desk's priority matrix), the
/// estimate, the billing type, and who solved it.
/// </summary>
public sealed partial class DefaultTicketService
{
    /// <summary>
    /// The longest estimate a ticket may have, in minutes (1,000 hours).
    /// </summary>
    public const int MaxEstimatedMinutes = 60 * 1000;

    // The checks of the planning values, made before anything changes.
    private void CheckPlanning(TicketUpdate update, List<string> errors)
    {
        if ((update.SetEstimate || update.SetPlanning) && update.EstimatedMinutes is { } minutes && (minutes < 1 || minutes > MaxEstimatedMinutes))
        {
            errors.Add(S["The estimate must be from a minute to 1,000 hours."]);
        }
    }

    // A new ticket's planning values, and the priority the desk's matrix gives them when the request names none.
    private static void ApplyPlanning(TicketPart part, TicketCreateRequest request)
    {
        part.Impact = request.Impact;
        part.Urgency = request.Urgency;
        part.EstimatedStartUtc = request.EstimatedStartUtc;
        part.EstimatedMinutes = request.EstimatedMinutes is > 0 and <= MaxEstimatedMinutes ? request.EstimatedMinutes : null;
        part.BillingType = request.BillingType;
    }

    // Applies the planning values of an update. Impact or urgency that changed set the priority from the desk's matrix
    // when the update names no priority of its own.
    private async Task ApplyPlanningAsync(ContentItem ticket, TicketPart part, TicketUpdate update, List<TimelineFieldChange> changes, CancellationToken cancellationToken)
    {
        var impact = update.SetPlanning ? update.Impact : update.Impact ?? part.Impact;
        var urgency = update.SetPlanning ? update.Urgency : update.Urgency ?? part.Urgency;
        var levelsChanged = false;

        if (impact != part.Impact)
        {
            changes.Add(new TimelineFieldChange { Field = S["Impact"], Key = TicketChangeKeys.Impact, From = LevelName(part.Impact), To = LevelName(impact), FromValue = part.Impact?.ToString(), ToValue = impact?.ToString() });
            part.Impact = impact;
            levelsChanged = true;
        }

        if (urgency != part.Urgency)
        {
            changes.Add(new TimelineFieldChange { Field = S["Urgency"], Key = TicketChangeKeys.Urgency, From = LevelName(part.Urgency), To = LevelName(urgency), FromValue = part.Urgency?.ToString(), ToValue = urgency?.ToString() });
            part.Urgency = urgency;
            levelsChanged = true;
        }

        if (levelsChanged
            && string.IsNullOrEmpty(update.PriorityId)
            && await _desks.FindByIdAsync(part.DeskId, cancellationToken) is { } desk
            && desk.PriorityFor(part.Impact, part.Urgency) is { } priorityId
            && priorityId != part.PriorityId
            && await _priorities.FindByIdAsync(priorityId, cancellationToken) is { } priority)
        {
            await SetPriorityAsync(ticket, part, priority, changes, cancellationToken);
        }

        var billingType = update.SetPlanning ? update.BillingType : update.BillingType ?? part.BillingType;

        if (billingType != part.BillingType)
        {
            changes.Add(new TimelineFieldChange { Field = S["Billing type"], Key = TicketChangeKeys.BillingType, From = BillingTypeName(part.BillingType), To = BillingTypeName(billingType), FromValue = part.BillingType?.ToString(), ToValue = billingType?.ToString() });
            part.BillingType = billingType;
        }

        if ((update.SetEstimate || update.SetPlanning) && (update.EstimatedStartUtc != part.EstimatedStartUtc || update.EstimatedMinutes != part.EstimatedMinutes))
        {
            changes.Add(new TimelineFieldChange
            {
                Field = S["Estimate"],
                Key = TicketChangeKeys.Estimate,
                From = await EstimateTextAsync(part.EstimatedStartUtc, part.EstimatedMinutes),
                To = await EstimateTextAsync(update.EstimatedStartUtc, update.EstimatedMinutes),
                FromValue = part.EstimatedMinutes?.ToString(CultureInfo.InvariantCulture),
                ToValue = update.EstimatedMinutes?.ToString(CultureInfo.InvariantCulture),
            });
            part.EstimatedStartUtc = update.EstimatedStartUtc;
            part.EstimatedMinutes = update.EstimatedMinutes;
        }
    }

    // Gives the ticket a priority, on its timeline, and retargets its running timers to it.
    private async Task SetPriorityAsync(ContentItem ticket, TicketPart part, TicketPriority priority, List<TimelineFieldChange> changes, CancellationToken cancellationToken)
    {
        var subject = SubjectOf(ticket.ContentItemId);
        var from = await _priorities.FindByIdAsync(part.PriorityId, cancellationToken);

        changes.Add(new TimelineFieldChange { Field = S["Priority"], Key = TicketChangeKeys.Priority, From = from?.Name, To = priority.Name, FromValue = part.PriorityId, ToValue = priority.ItemId });
        part.PriorityId = priority.ItemId;
        part.PriorityRank = priority.Rank;

        foreach (var timer in await _serviceLevels.GetTimersAsync(subject, activeOnly: true, cancellationToken))
        {
            await _serviceLevels.RetargetAsync(subject, timer.PolicyId, priority.ItemId, cancellationToken);

            break;
        }
    }

    // The agreement a new ticket works under: the one the request names, or the account's that covers it.
    private async Task<(TicketAgreementInfo Agreement, string Error)> ResolveAgreementAsync(TicketCreateRequest request, TicketPart part, CancellationToken cancellationToken)
    {
        if (_serviceProvider.GetService<ITicketAgreementResolver>() is not { } agreements)
        {
            return (null, null);
        }

        if (!string.IsNullOrEmpty(request.AgreementId))
        {
            var named = await agreements.FindAsync(request.AgreementId, cancellationToken);

            return named is null || named.AccountId != part.AccountId
                ? (null, S["The agreement doesn't exist or isn't with the ticket's account."])
                : (named, null);
        }

        return string.IsNullOrEmpty(part.AccountId)
            ? (null, null)
            : (await agreements.FindForNewTicketAsync(part.AccountId, part.DeskId, part.ContentItem?.ContentType, cancellationToken), null);
    }

    // The check of a change of agreement, made before anything changes.
    private async Task CheckAgreementAsync(TicketPart part, TicketUpdate update, List<string> errors, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(update.AgreementId) || update.AgreementId == part.AgreementId)
        {
            return;
        }

        var agreement = _serviceProvider.GetService<ITicketAgreementResolver>() is { } agreements
            ? await agreements.FindAsync(update.AgreementId, cancellationToken)
            : null;

        if (agreement is null || agreement.AccountId != part.AccountId)
        {
            errors.Add(S["The agreement doesn't exist or isn't with the ticket's account."]);
        }
    }

    // Changes the agreement, on the timeline; the ticket's timers follow the new agreement's policy when it brings one.
    private async Task ApplyAgreementAsync(ContentItem ticket, TicketPart part, TicketUpdate update, List<TimelineFieldChange> changes, CancellationToken cancellationToken)
    {
        if (update.AgreementId is null || (update.AgreementId.Length == 0 ? part.AgreementId is null : update.AgreementId == part.AgreementId))
        {
            return;
        }

        var agreements = _serviceProvider.GetService<ITicketAgreementResolver>();
        var from = agreements is null || string.IsNullOrEmpty(part.AgreementId) ? null : await agreements.FindAsync(part.AgreementId, cancellationToken);
        var to = agreements is null || update.AgreementId.Length == 0 ? null : await agreements.FindAsync(update.AgreementId, cancellationToken);

        changes.Add(new TimelineFieldChange
        {
            Field = S["Agreement"],
            Key = TicketChangeKeys.Agreement,
            From = from?.Name ?? part.AgreementId,
            To = to?.Name,
            FromValue = part.AgreementId,
            ToValue = to?.AgreementId,
        });
        part.AgreementId = to?.AgreementId;

        if (!string.IsNullOrEmpty(to?.ServiceLevelPolicyId))
        {
            var subject = SubjectOf(ticket.ContentItemId);

            await _serviceLevels.RetargetAsync(subject, to.ServiceLevelPolicyId, part.PriorityId, cancellationToken);
        }
    }

    // Keeps who solved the ticket, when this change solved it and a person made it.
    private static void KeepSolver(TicketPart part, DateTime? solvedBefore, TicketActor actor)
    {
        if (part.StatusCategory == TicketStatusCategory.Solved && part.SolvedUtc != solvedBefore)
        {
            part.SolvedById = actor?.Party is { IsEmpty: false, Kind: PartyKinds.User } party ? party.Id : null;
        }
    }

    private string LevelName(TicketLevel? level)
        => level switch
        {
            TicketLevel.Low => S["Low"],
            TicketLevel.Medium => S["Medium"],
            TicketLevel.High => S["High"],
            _ => null,
        };

    private string BillingTypeName(TicketBillingType? type)
        => type switch
        {
            TicketBillingType.Billable => S["Billable"],
            TicketBillingType.NotBillable => S["Not billable"],
            TicketBillingType.Agreement => S["Covered by the agreement"],
            TicketBillingType.NoCharge => S["No charge"],
            _ => null,
        };

    // The estimate as people read it, its start in the local time zone (the user's or the site's).
    private async Task<string> EstimateTextAsync(DateTime? startUtc, int? minutes)
    {
        string start = null;

        if (startUtc is { } utc && _serviceProvider.GetService<ILocalClock>() is { } localClock)
        {
            start = (await localClock.ConvertToLocalAsync(new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)))).ToString("g", CultureInfo.CurrentCulture);
        }

        var length = minutes is { } value
            ? value < 60 ? S["{0} min", value].Value : S["{0} h {1:00} min", value / 60, value % 60].Value
            : null;

        return (start, length) switch
        {
            (null, null) => null,
            (null, _) => length,
            (_, null) => S["from {0}", start],
            _ => S["{0}, from {1}", length, start],
        };
    }
}
