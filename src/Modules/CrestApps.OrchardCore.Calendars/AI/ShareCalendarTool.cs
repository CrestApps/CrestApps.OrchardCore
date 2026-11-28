using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Services;

namespace CrestApps.OrchardCore.Calendars.AI;

/// <summary>
/// Replaces who a calendar is shared with, as the calendar editor's Sharing card does.
/// </summary>
internal sealed class ShareCalendarTool : CalendarAIToolBase
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public const string ToolName = "shareCalendar";

    /// <summary>
    /// The most shares a calendar may be given in one call.
    /// </summary>
    public const int MaxShares = 100;

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "calendarId": { "type": "string", "description": "The calendar's ID." },
        "shares": {
          "type": "array",
          "description": "Everyone the calendar is shared with after the call. This list replaces the current one: an empty list removes every share.",
          "items": {
            "type": "object",
            "properties": {
              "kind": { "type": "string", "enum": ["User", "Role"], "description": "Whether the share is for one user or for everyone in a role." },
              "id": { "type": "string", "description": "The user ID, or the role name." },
              "access": { "type": "string", "enum": ["FreeBusy", "Read", "Write", "Manage"], "description": "FreeBusy: sees when it is busy. Read: sees its events. Write: adds and changes events. Manage: also changes the calendar and its sharing." }
            },
            "required": ["kind", "id", "access"],
            "additionalProperties": false
          }
        }
      },
      "required": ["calendarId", "shares"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description => "Replaces who a calendar is shared with: users and roles at free/busy, read, write or manage level. Read the current shares with getCalendar first, because the list you give replaces them.";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _schema;

    /// <inheritdoc />
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var (calendar, level) = await CalendarToolSupport.FindCalendarAsync(services, user, Read(arguments, "calendarId"), cancellationToken);

        if (calendar is null)
        {
            return CalendarToolSupport.CalendarNotFound;
        }

        if (level < CalendarAccessLevel.Manage)
        {
            return "The user can't change who that calendar is shared with: it needs the right to manage it.";
        }

        var items = CalendarToolSupport.ReadObjects(arguments, "shares");

        if (items is null)
        {
            return "Give shares as a list of { kind, id, access }. Give an empty list to stop sharing the calendar.";
        }

        if (items.Count > MaxShares)
        {
            return $"Give at most {MaxShares} shares.";
        }

        var shares = new List<CalendarShare>();

        foreach (var item in items)
        {
            var id = CalendarToolSupport.GetText(item, "id");

            if (id is null ||
                !Enum.TryParse<CalendarPrincipalKind>(CalendarToolSupport.GetText(item, "kind"), ignoreCase: true, out var kind) ||
                !Enum.TryParse<CalendarAccessLevel>(CalendarToolSupport.GetText(item, "access"), ignoreCase: true, out var access) ||
                access == CalendarAccessLevel.None)
            {
                return "Each share needs a kind (User or Role), an id and an access level (FreeBusy, Read, Write or Manage).";
            }

            if (shares.Any(share => share.PrincipalKind == kind && string.Equals(share.PrincipalId, id, StringComparison.OrdinalIgnoreCase)))
            {
                return $"'{id}' is listed twice. Give each user and role once, with the access it should have.";
            }

            shares.Add(new CalendarShare { PrincipalKind = kind, PrincipalId = id, Access = access });
        }

        var error = await ValidatePrincipalsAsync(services, shares);

        if (error is not null)
        {
            return error;
        }

        var previous = calendar.Shares;

        calendar.Shares = shares;

        var result = await services.GetRequiredService<ICalendarService>().UpdateAsync(calendar, new CalendarChangeOptions { Actor = CalendarUser.GetId(user) }, cancellationToken);

        if (!result.Succeeded)
        {
            calendar.Shares = previous;

            return "The sharing was not saved: " + CalendarToolSupport.Describe(result.Errors);
        }

        var levelAfter = await services.GetRequiredService<ICalendarAccessService>().GetAccessLevelAsync(user, result.Value ?? calendar, cancellationToken);

        return CalendarToolSupport.Json(new
        {
            calendarId = calendar.ItemId,
            calendar = calendar.Name,
            shares = shares.Select(share => new { kind = share.PrincipalKind.ToString(), id = share.PrincipalId, access = share.Access.ToString() }),
            yourAccess = levelAfter.ToString(),
            note = levelAfter < CalendarAccessLevel.Manage ? "The user no longer manages this calendar." : null,
        });
    }

    // The editor offers only existing users and every role but Anonymous.
    private static async Task<string> ValidatePrincipalsAsync(IServiceProvider services, List<CalendarShare> shares)
    {
        var roleNames = shares.Where(share => share.PrincipalKind == CalendarPrincipalKind.Role).ToList();

        if (roleNames.Count > 0)
        {
            var roleService = services.GetService<IRoleService>();
            IEnumerable<string> roles = roleService is null ? [] : await roleService.GetRoleNamesAsync();
            var known = roles.Where(role => !string.Equals(role, "Anonymous", StringComparison.OrdinalIgnoreCase)).ToList();

            foreach (var share in roleNames)
            {
                var role = known.FirstOrDefault(name => string.Equals(name, share.PrincipalId, StringComparison.OrdinalIgnoreCase));

                if (role is null)
                {
                    return $"There is no role '{share.PrincipalId}' a calendar can be shared with.";
                }

                share.PrincipalId = role;
            }
        }

        var userIds = shares.Where(share => share.PrincipalKind == CalendarPrincipalKind.User).Select(share => share.PrincipalId).ToList();

        if (userIds.Count > 0)
        {
            var userLookup = services.GetService<CalendarUserLookup>();
            IReadOnlyList<CalendarUserItem> users = userLookup is null ? [] : await userLookup.GetAsync(userIds);
            var missing = userIds.FirstOrDefault(id => !users.Any(found => string.Equals(found.Value, id, StringComparison.Ordinal)));

            if (missing is not null)
            {
                return $"There is no user with the ID '{missing}'. Give user IDs, not names or emails.";
            }
        }

        return null;
    }
}
