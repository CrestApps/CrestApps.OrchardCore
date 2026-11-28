using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Staff.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Staff.AI;

/// <summary>
/// Ends a staff member's employment the way Staff itself does: the status becomes <see cref="StaffStatus.Terminated"/>
/// and the staff manager saves it, so its handlers record the history and tell every <see cref="IStaffLifecycleObserver"/>
/// (shifts, visits, the Contact Center, phone extensions, the Staff role) to offboard the person, as when the staff editor
/// sets the status, or the Staff upkeep background task does after a last working day. A last working day still to come
/// is only recorded; that task terminates the person once it has passed. Like the staff editor, it needs the person to be
/// among the people the caller manages. It asks for approval: the offboarding cannot be undone. So it acts only on a
/// person named without doubt: by staff member id, or by an e-mail address or employee number that exactly matches one
/// person. A name, or anything that matches several people, only lists who matches, with their ids, and changes nothing.
/// </summary>
internal sealed class TerminateStaffMemberTool : StaffManagementAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "terminateStaffMember";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "staffMemberId": {
          "type": "string",
          "description": "The id of the staff member, from getStaffMember."
        },
        "employeeNumber": {
          "type": "string",
          "description": "The person's exact employee number, when the id is not known."
        },
        "email": {
          "type": "string",
          "description": "The person's exact e-mail address, when the id is not known. It must match exactly one person."
        },
        "name": {
          "type": "string",
          "description": "Only to look the person up: a name never terminates anyone. The people who match are listed with their ids, and nothing is changed; call again with the staffMemberId of the one meant."
        },
        "lastWorkingDay": {
          "type": "string",
          "description": "The person's last working day, as yyyy-MM-dd, in their own time zone. A day still to come (today included) is only recorded: the person is terminated automatically once it has passed. Leave it out, or give a day already past, to terminate them now."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Ends a staff member's employment. Without a last working day, or with one already past, the person is terminated now and the other modules offboard them as when the staff editor sets the status to Terminated: published shifts after their last day come off the schedule, visits not started go back to the dispatch board, the Contact Center signs them out and their phone extension is freed. With a last working day still to come, the day is recorded and the person is terminated automatically after it. It cannot be undone, so it needs the staffMemberId, or the exact e-mail address or employee number of one person; a name only lists who matches.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Task<bool> IsAllowedAsync(IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
        => ManagesAnybodyAsync(services, caller, cancellationToken);

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var scopeService = services.GetRequiredService<IStaffScopeService>();

        // Only the people the caller manages are found, as on the staff editor; the answer is the same whether someone
        // outside the scope exists or not.
        var (staff, error) = await FindStaffToTerminateAsync(arguments, services, person => scopeService.CanManageAsync(caller, person, cancellationToken), cancellationToken);

        if (staff is null)
        {
            return $"Nothing was changed: {error}";
        }

        if (!TryParseDate(ReadString(arguments, "lastWorkingDay"), out var lastWorkingDay))
        {
            return "Nothing was changed: lastWorkingDay must be a date written as yyyy-MM-dd.";
        }

        if (staff.Status == StaffStatus.Terminated)
        {
            return Json(new
            {
                id = staff.ItemId,
                name = staff.DisplayName,
                status = staff.Status.ToString(),
                terminated = false,
                lastWorkingDay = Date(staff.TerminationDate),
                message = $"Nothing was changed: {staff.DisplayName} is already terminated.",
            });
        }

        // Today in the person's own time zone, the day the Staff upkeep task compares the last working day with.
        var timeZoneId = await services.GetRequiredService<IStaffTimeZoneResolver>().GetTimeZoneIdAsync(staff, cancellationToken);
        var today = DateOnly.FromDateTime(CalendarTime.ToLocal(services.GetRequiredService<IClock>().UtcNow, timeZoneId));
        var now = lastWorkingDay is null || lastWorkingDay < today;

        if (lastWorkingDay is { } day)
        {
            staff.TerminationDate = day;
        }
        else if (staff.TerminationDate is not { } planned || planned > today)
        {
            // Leaving now: today is the last working day, unless a day already past was planned.
            staff.TerminationDate = today;
        }

        if (now)
        {
            staff.Status = StaffStatus.Terminated;
        }

        var manager = services.GetRequiredService<IStaffMemberManager>();
        var validation = await manager.ValidateAsync(staff, cancellationToken);

        if (!validation.Succeeded)
        {
            return $"Nothing was changed: {string.Join(" ", validation.Errors.Select(item => item.ErrorMessage).Where(message => !string.IsNullOrEmpty(message)))}";
        }

        await manager.UpdateAsync(staff, cancellationToken: cancellationToken);

        return Json(new
        {
            id = staff.ItemId,
            name = staff.DisplayName,
            status = staff.Status.ToString(),
            terminated = now,
            lastWorkingDay = Date(staff.TerminationDate),
            timeZoneId,
            message = now
                ? $"{staff.DisplayName} has been terminated. The other modules offboard them now."
                : $"{staff.DisplayName}'s last working day is {Date(staff.TerminationDate)}. They stay {staff.Status} until then and are terminated automatically after it. To cancel, clear the date with saveStaffMember.",
        });
    }

    // Termination cannot be undone, so the person must be named without doubt: by id, or by an exact e-mail address or
    // employee number that matches one person the caller may manage. A name, or a value that matches nobody or several
    // people, changes nothing; a name lists who matches, with their ids, for the caller to choose from.
    private static async Task<(StaffMember Staff, string Error)> FindStaffToTerminateAsync(AIFunctionArguments arguments, IServiceProvider services, Func<StaffMember, Task<bool>> canManage, CancellationToken cancellationToken)
    {
        var manager = services.GetRequiredService<IStaffMemberManager>();

        if (ReadString(arguments, "staffMemberId") is { } id)
        {
            return await manager.FindByIdAsync(id, cancellationToken) is { } byId && await canManage(byId)
                ? (byId, null)
                : (null, $"No staff member you may see has the id '{id}'. Use getStaffMember with a name to find them.");
        }

        if (ReadString(arguments, "employeeNumber") is { } number)
        {
            // Employee numbers are unique, and the store matches the whole number.
            return await manager.FindByEmployeeNumberAsync(number, cancellationToken) is { } byNumber && await canManage(byNumber)
                ? (byNumber, null)
                : (null, $"No staff member you may see has the employee number '{number}'.");
        }

        if (ReadString(arguments, "email") is { } email)
        {
            var (complete, visible) = await FindVisibleAsync(manager, email, canManage, cancellationToken);

            // Only a search read to its end can tell that exactly one person has the address.
            if (!complete)
            {
                return (null, $"Too many staff members match '{email}' to tell whether exactly one has that e-mail address. Give the staffMemberId instead.");
            }

            var exact = visible
                .Where(staff => string.Equals(staff.Email?.Trim(), email, StringComparison.OrdinalIgnoreCase))
                .ToList();

            return exact.Count switch
            {
                1 => (exact[0], null),
                0 => (null, $"No staff member you may see has the e-mail address '{email}'. Give the staffMemberId instead."),
                _ => (null, $"Several staff members have the e-mail address '{email}': {DescribeCandidates(exact)}. Give the staffMemberId of the one you mean."),
            };
        }

        if (ReadString(arguments, "name") is { } name)
        {
            var (_, matches) = await FindVisibleAsync(manager, name, canManage, cancellationToken);

            return matches.Count == 0
                ? (null, $"No staff member you may see matches '{name}'.")
                : (null, $"A name is not enough to end someone's employment. People who match '{name}': {DescribeCandidates(matches)}. Call again with the staffMemberId of the one you mean.");
        }

        return (null, "Give the staffMemberId, or the exact e-mail address or employee number, of the staff member.");
    }

    // The people the search finds whom the caller may manage, and whether the search was read to its end.
    private static async Task<(bool Complete, List<StaffMember> Visible)> FindVisibleAsync(IStaffMemberManager manager, string search, Func<StaffMember, Task<bool>> canManage, CancellationToken cancellationToken)
    {
        var (count, found) = await manager.SearchAsync(new StaffQuery { Search = search }, 0, MaxPageSize, cancellationToken);
        var visible = new List<StaffMember>();

        foreach (var staff in found)
        {
            if (await canManage(staff))
            {
                visible.Add(staff);
            }
        }

        return (count <= found.Count, visible);
    }

    private static string DescribeCandidates(IEnumerable<StaffMember> candidates)
        => string.Join("; ", candidates.Take(10).Select(staff => $"{staff.DisplayName} ({staff.JobTitle ?? "no job title"}, id {staff.ItemId})"));
}
