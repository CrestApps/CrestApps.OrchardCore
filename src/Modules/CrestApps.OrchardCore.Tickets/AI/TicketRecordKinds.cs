using System.ComponentModel.DataAnnotations;
using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.BusinessHours.Services;
using CrestApps.OrchardCore.ServiceLevels.Services;
using CrestApps.OrchardCore.Teams.Services;
using CrestApps.OrchardCore.Tickets.Core.Services;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using CrestApps.OrchardCore.Tickets.TimeTracker;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement.Metadata;

namespace CrestApps.OrchardCore.Tickets.AI;

/// <summary>
/// The ticket setup records the generic record tools list, read and write: desks, statuses, priorities and saved views.
/// Writes replay the same editors as the Service Desk settings pages, and the rules of
/// <c>TicketSettingsRecordHandler</c> run through the catalog manager as on those pages. A few rules apply only to the
/// chat: a desk's AI profile is never changed, a desk never starts erasing protected information sooner, and the ids
/// the pages offer only through pickers must exist.
/// </summary>
internal static class TicketRecordKinds
{
    /// <summary>
    /// The name of the desk kind.
    /// </summary>
    public const string DeskKind = "ticketDesk";

    /// <summary>
    /// The name of the status kind.
    /// </summary>
    public const string StatusKind = "ticketStatus";

    /// <summary>
    /// The name of the priority kind.
    /// </summary>
    public const string PriorityKind = "ticketPriority";

    /// <summary>
    /// The name of the saved view kind.
    /// </summary>
    public const string ViewKind = "ticketView";

    /// <summary>
    /// The allow-listed fields of a desk. The settings other features keep on a desk are named only.
    /// </summary>
    /// <param name="desk">The desk.</param>
    /// <returns>The fields.</returns>
    public static object DescribeDesk(TicketDesk desk)
    {
        var ai = desk.TryGet<TicketDeskAISettings>(out var aiSettings) ? aiSettings : null;
        var time = desk.TryGet<TicketDeskTimeSettings>(out var timeSettings) ? timeSettings : null;

        return new
        {
            id = desk.ItemId,
            name = desk.Name,
            description = desk.Description,
            audience = desk.Audience.ToString(),
            defaultTeamId = desk.DefaultTeamId,
            hoursScheduleId = desk.HoursScheduleId,
            serviceLevelPolicyId = desk.ServiceLevelPolicyId,
            ticketTypes = desk.TicketTypes ?? [],
            reopenWindowDays = desk.ReopenWindowDays,
            autoCloseDays = desk.AutoCloseDays,
            deleteProtectedInformationDays = desk.DeleteProtectedInformationDays,
            assignmentStrategy = desk.AssignmentStrategy,
            notifyRequester = desk.NotifyRequester,
            notifyAssignee = desk.NotifyAssignee,
            ratingRequestDelayHours = desk.RatingRequestDelayHours,
            availabilitySources = desk.AvailabilitySources,
            priorityMatrix = (desk.PriorityMatrix ?? [])
                .Where(rule => rule is not null)
                .Select(rule => new
                {
                    impact = rule.Impact.ToString(),
                    urgency = rule.Urgency.ToString(),
                    priorityId = rule.PriorityId,
                })
                .ToArray(),
            ai = ai is null
                ? null
                : new
                {
                    profileId = ai.Disabled ? null : ai.ProfileId,
                    off = ai.Disabled,
                    applyPriority = !ai.Disabled && ai.ApplyPriority,
                },
            time = time is null
                ? null
                : new
                {
                    stageId = time.StageId,
                    notBilledStageId = time.NotBilledStageId,
                },
            otherSettings = desk.Properties?.Keys
                .Where(key => key is not nameof(TicketDeskAISettings) and not nameof(TicketDeskTimeSettings))
                .Order(StringComparer.Ordinal)
                .ToArray() ?? [],
        };
    }

    /// <summary>
    /// The allow-listed fields of a status.
    /// </summary>
    /// <param name="status">The status.</param>
    /// <returns>The fields.</returns>
    public static object DescribeStatus(TicketStatus status)
        => new
        {
            id = status.ItemId,
            name = status.Name,
            category = status.Category.ToString(),
            customerLabel = status.CustomerLabel,
            pausesTimers = status.PausesTimers,
            color = status.Color,
            order = status.Order,
        };

    /// <summary>
    /// The allow-listed fields of a priority.
    /// </summary>
    /// <param name="priority">The priority.</param>
    /// <returns>The fields.</returns>
    public static object DescribePriority(TicketPriority priority)
        => new
        {
            id = priority.ItemId,
            name = priority.Name,
            rank = priority.Rank,
            color = priority.Color,
        };

    /// <summary>
    /// The allow-listed fields of a saved view. Its owner is not named; <c>personal</c> says whether it has one.
    /// </summary>
    /// <param name="view">The view.</param>
    /// <returns>The fields.</returns>
    public static object DescribeView(TicketView view)
    {
        var query = view.Query ?? new TicketQuery();

        return new
        {
            id = view.ItemId,
            name = view.Name,
            order = view.Order,
            sharedWithTeamId = view.SharedWithTeamId,
            personal = !string.IsNullOrEmpty(view.OwnerId),
            categories = (query.Categories ?? []).Select(category => category.ToString()).ToArray(),
            deskIds = query.DeskIds ?? [],
            assignee = query.Assignee,
            dueWithinHours = query.DueWithin is { } within ? (int?)within.TotalHours : null,
            sort = query.Sort.ToString(),
        };
    }

    /// <summary>
    /// Says why a desk cannot be deleted, as the Desks page does: tickets or ticket types still use it.
    /// </summary>
    /// <param name="services">The services of the tool call.</param>
    /// <param name="desk">The desk.</param>
    /// <param name="cancellationToken">A token to cancel the check.</param>
    /// <returns>Why it is kept, or <see langword="null"/>.</returns>
    public static async ValueTask<string> GetDeskUseAsync(IServiceProvider services, TicketDesk desk, CancellationToken cancellationToken)
        => InUse(desk.Name, await services.GetRequiredService<TicketSettingsUsage>().GetUsagesAsync(desk, cancellationToken));

    /// <summary>
    /// Says why a status cannot be deleted, as the Statuses page does: tickets still have it.
    /// </summary>
    /// <param name="services">The services of the tool call.</param>
    /// <param name="status">The status.</param>
    /// <param name="cancellationToken">A token to cancel the check.</param>
    /// <returns>Why it is kept, or <see langword="null"/>.</returns>
    public static async ValueTask<string> GetStatusUseAsync(IServiceProvider services, TicketStatus status, CancellationToken cancellationToken)
        => InUse(status.Name, await services.GetRequiredService<TicketSettingsUsage>().GetUsagesAsync(status, cancellationToken));

    /// <summary>
    /// Says why a priority cannot be deleted: tickets or ticket types use it, as the Priorities page checks; a desk's
    /// priority matrix names it, which would make that desk fail its rules on every later save; a ticket category gives it
    /// to its new tickets, which would fall back to Normal; or a tickets service level policy sets a target for it, which
    /// would no longer apply to any ticket.
    /// </summary>
    /// <param name="services">The services of the tool call.</param>
    /// <param name="priority">The priority.</param>
    /// <param name="cancellationToken">A token to cancel the check.</param>
    /// <returns>Why it is kept, or <see langword="null"/>.</returns>
    public static async ValueTask<string> GetPriorityUseAsync(IServiceProvider services, TicketPriority priority, CancellationToken cancellationToken)
    {
        var usages = new List<string>(await services.GetRequiredService<TicketSettingsUsage>().GetUsagesAsync(priority, cancellationToken));

        foreach (var desk in await services.GetRequiredService<ICatalog<TicketDesk>>().GetAllAsync(cancellationToken))
        {
            if (desk.PriorityMatrix?.Any(rule => string.Equals(rule?.PriorityId, priority.ItemId, StringComparison.Ordinal)) == true)
            {
                usages.Add($"the priority matrix of the desk {desk.Name}");
            }
        }

        // The categories of every ticket type: a category's priority is what its new tickets get.
        if (services.GetService<ITicketCategoryService>() is { } categories)
        {
            var named = new HashSet<string>(StringComparer.Ordinal);

            foreach (var type in await TicketTypesAsync(services))
            {
                foreach (var category in await categories.ListAsync(type, cancellationToken: cancellationToken))
                {
                    if (string.Equals(category.PriorityId, priority.ItemId, StringComparison.Ordinal) && named.Add(category.Id))
                    {
                        usages.Add($"the ticket category {category.PathText ?? category.Name}");
                    }
                }
            }
        }

        // The targets of the tickets service level policies are keyed by the priority's id.
        if (services.GetService<IServiceLevelPolicyManager>() is { } policies)
        {
            foreach (var policy in await policies.ListByScopeAsync(TicketsConstants.ServiceLevelScope, cancellationToken))
            {
                if (policy.Targets?.Any(target => target is not null && string.Equals(target.Priority, priority.ItemId, StringComparison.OrdinalIgnoreCase)) == true)
                {
                    usages.Add($"the targets of the service level policy {policy.Name}");
                }
            }
        }

        return InUse(priority.Name, usages);
    }

    private static async Task<IReadOnlyList<string>> TicketTypesAsync(IServiceProvider services)
        => [.. (await services.GetRequiredService<IContentDefinitionManager>().ListTypeDefinitionsAsync())
            .Where(type => type.Parts.Any(part => part.PartDefinition.Name == TicketsConstants.PartName))
            .Select(type => type.Name)];

    /// <summary>
    /// Validates a desk as its page does, then applies the rules of the chat: its AI settings stay as they are, it never
    /// starts erasing protected information sooner, and a team, hours schedule, service level policy or ticket type it
    /// is given must exist.
    /// </summary>
    /// <param name="services">The services of the tool call.</param>
    /// <param name="desk">The desk with the values the editor bound.</param>
    /// <param name="cancellationToken">A token to cancel the checks.</param>
    /// <returns>The validation result.</returns>
    public static async ValueTask<ValidationResultDetails> ValidateDeskAsync(IServiceProvider services, TicketDesk desk, CancellationToken cancellationToken)
    {
        var manager = services.GetRequiredService<ICatalogManager<TicketDesk>>();
        var result = await manager.ValidateAsync(desk, cancellationToken) ?? new ValidationResultDetails();
        var stored = string.IsNullOrEmpty(desk.ItemId) ? null : await manager.FindByIdAsync(desk.ItemId, cancellationToken);

        // Which AI profile replies to and triages a desk's tickets is AI configuration: the assistant may not rewire it.
        if (AISettingsOf(stored) != AISettingsOf(desk))
        {
            result.Fail(new ValidationResult(
                "The desk's AI settings (its AI profile, whether AI is off and whether AI sets the priority) are not changed from the chat. A person changes them on the desk's page.",
                [nameof(TicketDeskAISettings)]));
        }

        // A desk that erases protected information, or erases it sooner, erases what cannot be brought back, and a chat
        // save cannot ask for approval. Keeping it longer, or keeping it, is allowed. A new desk counts as one that keeps
        // it (0 days), as a new desk has the default AI settings above.
        var storedDays = stored?.DeleteProtectedInformationDays ?? 0;

        if (desk.DeleteProtectedInformationDays > 0
            && (storedDays <= 0 || desk.DeleteProtectedInformationDays < storedDays))
        {
            result.Fail(new ValidationResult(
                "Erasing a desk's protected ticket information sooner can't be undone, so it is not changed from the chat. A person changes it on the desk's page; from the chat it can only be kept longer or kept.",
                [nameof(TicketDesk.DeleteProtectedInformationDays)]));
        }

        // The page offers these only through pickers; an id the chat gives must exist. An id the desk already had is
        // left alone, so a desk whose team was deleted can still be changed otherwise, as on the page.
        if (Changed(stored?.DefaultTeamId, desk.DefaultTeamId)
            && services.GetService<ITeamManager>() is { } teams
            && await teams.FindByIdAsync(desk.DefaultTeamId, cancellationToken) is null)
        {
            result.Fail(new ValidationResult($"No team has the id '{desk.DefaultTeamId}'. Use listRecords with the kind team to find one.", [nameof(TicketDesk.DefaultTeamId)]));
        }

        if (Changed(stored?.HoursScheduleId, desk.HoursScheduleId)
            && services.GetService<IOpenHoursService>() is { } hours
            && !(await hours.ListSchedulesAsync(cancellationToken)).Any(schedule => string.Equals(schedule.Id, desk.HoursScheduleId, StringComparison.Ordinal)))
        {
            result.Fail(new ValidationResult($"No business hours schedule has the id '{desk.HoursScheduleId}'. Leave it empty to count around the clock.", [nameof(TicketDesk.HoursScheduleId)]));
        }

        // A desk's tickets are measured by a policy of the tickets scope, the only ones its page offers.
        if (Changed(stored?.ServiceLevelPolicyId, desk.ServiceLevelPolicyId)
            && services.GetService<IServiceLevelPolicyManager>() is { } policies
            && (await policies.FindByIdAsync(desk.ServiceLevelPolicyId, cancellationToken) is not { } policy
                || !string.Equals(policy.Scope, TicketsConstants.ServiceLevelScope, StringComparison.OrdinalIgnoreCase)))
        {
            result.Fail(new ValidationResult($"No ticket service level policy has the id '{desk.ServiceLevelPolicyId}'. Use listRecords with the kind serviceLevelPolicy to find one whose scope is {TicketsConstants.ServiceLevelScope}.", [nameof(TicketDesk.ServiceLevelPolicyId)]));
        }

        var added = (desk.TicketTypes ?? []).Except(stored?.TicketTypes ?? [], StringComparer.OrdinalIgnoreCase).ToList();

        if (added.Count > 0)
        {
            var ticketTypes = (await services.GetRequiredService<IContentDefinitionManager>().ListTypeDefinitionsAsync())
                .Where(type => type.Parts.Any(part => part.PartDefinition.Name == TicketsConstants.PartName))
                .Select(type => type.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var unknown = added.Where(type => !ticketTypes.Contains(type)).ToList();

            if (unknown.Count > 0)
            {
                result.Fail(new ValidationResult(
                    $"These are not ticket types: {string.Join(", ", unknown)}. Ticket types are: {string.Join(", ", ticketTypes.Order(StringComparer.OrdinalIgnoreCase))}.",
                    [nameof(TicketDesk.TicketTypes)]));
            }
        }

        return result;
    }

    /// <summary>
    /// Validates a saved view as its page does, then checks that a team or desk it is given exists, as the page offers
    /// them only through pickers.
    /// </summary>
    /// <param name="services">The services of the tool call.</param>
    /// <param name="view">The view with the values the editor bound.</param>
    /// <param name="cancellationToken">A token to cancel the checks.</param>
    /// <returns>The validation result.</returns>
    public static async ValueTask<ValidationResultDetails> ValidateViewAsync(IServiceProvider services, TicketView view, CancellationToken cancellationToken)
    {
        var manager = services.GetRequiredService<ICatalogManager<TicketView>>();
        var result = await manager.ValidateAsync(view, cancellationToken) ?? new ValidationResultDetails();
        var stored = string.IsNullOrEmpty(view.ItemId) ? null : await manager.FindByIdAsync(view.ItemId, cancellationToken);

        if (Changed(stored?.SharedWithTeamId, view.SharedWithTeamId)
            && services.GetService<ITeamManager>() is { } teams
            && await teams.FindByIdAsync(view.SharedWithTeamId, cancellationToken) is null)
        {
            result.Fail(new ValidationResult($"No team has the id '{view.SharedWithTeamId}'. Use listRecords with the kind team to find one.", [nameof(TicketView.SharedWithTeamId)]));
        }

        var added = (view.Query?.DeskIds ?? []).Except(stored?.Query?.DeskIds ?? [], StringComparer.Ordinal).ToList();

        if (added.Count > 0)
        {
            var desks = (await services.GetRequiredService<ICatalog<TicketDesk>>().GetAllAsync(cancellationToken))
                .Select(desk => desk.ItemId)
                .ToHashSet(StringComparer.Ordinal);
            var unknown = added.Where(id => !desks.Contains(id)).ToList();

            if (unknown.Count > 0)
            {
                result.Fail(new ValidationResult($"No desk has the id {string.Join(", ", unknown)}. Use listRecords with the kind {DeskKind} to find one.", ["DeskIds"]));
            }
        }

        return result;
    }

    // The AI settings as they act: an AI turned off has no profile and sets no priority.
    private static (bool Off, string ProfileId, bool ApplyPriority) AISettingsOf(TicketDesk desk)
    {
        var settings = desk is not null && desk.TryGet<TicketDeskAISettings>(out var stored) ? stored : new TicketDeskAISettings();

        return settings.Disabled
            ? (true, null, false)
            : (false, string.IsNullOrWhiteSpace(settings.ProfileId) ? null : settings.ProfileId, settings.ApplyPriority);
    }

    private static bool Changed(string before, string after)
        => !string.IsNullOrEmpty(after) && !string.Equals(before, after, StringComparison.Ordinal);

    private static string InUse(string name, IReadOnlyCollection<string> usages)
        => usages.Count == 0
            ? null
            : $"{name} is still used by {string.Join(", ", usages)}, so it can't be deleted.";
}
