using System.Security.Claims;
using System.Text.Json;
using CrestApps.Core;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Services;
using Calendar = CrestApps.OrchardCore.Calendars.Models.Calendar;

namespace CrestApps.OrchardCore.Calendars.AI;

/// <summary>
/// Creates a calendar or changes one the person manages, with the fields of the calendar editor's General, Owner, Overlaps
/// and Details cards. Sharing, the feed and the subscription have their own tools.
/// </summary>
internal sealed class SaveCalendarTool : CalendarAIToolBase
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public const string ToolName = "saveCalendar";

    private static readonly string[] _resourceFields = ["location", "seats", "amenities"];
    private static readonly string[] _officeFields = ["address", "contact"];

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "calendarId": { "type": "string", "description": "The calendar to change. Leave out to create a calendar." },
        "type": { "type": "string", "description": "The calendar type of a new calendar, such as the type listCalendars shows for an existing one; the Calendars admin page lists every type. A calendar's type cannot change." },
        "name": { "type": "string", "description": "The name. Required for a new calendar." },
        "description": { "type": "string", "description": "The description. An empty text clears it." },
        "color": { "type": "string", "description": "The color, such as #1971c2. An empty text uses the type's color." },
        "timeZoneId": { "type": "string", "description": "The IANA time zone, such as America/New_York. An empty text uses the site's time zone." },
        "visibility": { "type": "string", "enum": ["Private", "Shared", "Public"], "description": "Private: only the owner and the people it is shared with. Shared: everyone sees when it is busy, not the details. Public: everyone sees its events." },
        "enabled": { "type": "boolean", "description": "Whether events can be added to the calendar." },
        "ownerId": { "type": "string", "description": "For a Team calendar, the role whose members it belongs to. For a Personal calendar, the user ID of its owner, which only a calendar administrator may set. Other types have no owner." },
        "limitOverlaps": { "type": "boolean", "description": "Whether to limit how many events may overlap." },
        "capacity": { "type": "integer", "minimum": 1, "description": "How many events may overlap when overlaps are limited." },
        "location": { "type": "string", "description": "Room and Equipment calendars only: where it is." },
        "seats": { "type": "integer", "minimum": 0, "description": "Room and Equipment calendars only: how many people it seats." },
        "amenities": { "type": "string", "description": "Room and Equipment calendars only: what it has, such as a projector." },
        "address": { "type": "string", "description": "Office calendars only: the office's address." },
        "contact": { "type": "string", "description": "Office calendars only: who to contact." }
      },
      "additionalProperties": false
    }
    """);

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description => "Creates a calendar of a given type or updates one: name, description, color, time zone, visibility, enabled, owner (user or role), overlap limit and capacity, and room, equipment or office details. Fields left out keep their value. It does not change sharing, the feed or the subscription.";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _schema;

    /// <inheritdoc />
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var calendarService = services.GetRequiredService<ICalendarService>();
        var accessService = services.GetRequiredService<ICalendarAccessService>();
        var typeManager = services.GetRequiredService<ICalendarTypeManager>();
        var calendarId = Read(arguments, "calendarId");
        var isNew = calendarId is null;
        Calendar calendar;

        if (isNew)
        {
            var typeName = Read(arguments, "type");

            if (typeName is null)
            {
                return "Give the type of the new calendar (listCalendars shows the type of each calendar, and the Calendars admin page lists every type), or a calendarId to change a calendar.";
            }

            var type = typeManager.FindType(typeName);

            if (type is null)
            {
                return $"There is no calendar type '{typeName}'. The Calendars admin page lists the types.";
            }

            if (!await accessService.CanCreateAsync(user, type.Name, cancellationToken))
            {
                return $"The user can't create {type.Name} calendars.";
            }

            calendar = await CalendarCreation.NewAsync(calendarService, type.Name, user, cancellationToken);

            if (calendar is null)
            {
                return $"A {type.Name} calendar can't be created.";
            }
        }
        else
        {
            var (found, level) = await CalendarToolSupport.FindCalendarAsync(services, user, calendarId, cancellationToken);

            if (found is null)
            {
                return CalendarToolSupport.CalendarNotFound;
            }

            if (level < CalendarAccessLevel.Manage)
            {
                return "The user can't change that calendar: it needs the right to manage it.";
            }

            if (Read(arguments, "type") is { } newType && !string.Equals(newType, found.Type, StringComparison.OrdinalIgnoreCase))
            {
                return "A calendar's type cannot change. Create a new calendar of that type instead.";
            }

            calendar = found;
        }

        // Every argument is checked before the calendar is touched, so a refused call leaves it as it was.
        var error = await ValidateAsync(arguments, services, user, calendar, isNew);

        if (error is not null)
        {
            return error;
        }

        // A team calendar is stored with the role's own spelling, as the Owner card's list of roles gives it.
        var ownerId = Read(arguments, "ownerId");

        if (ownerId is not null && calendar.OwnerKind == CalendarOwnerKind.Role)
        {
            ownerId = (await services.GetRequiredService<IRoleService>().GetRoleNamesAsync())
                .First(role => string.Equals(role, ownerId, StringComparison.OrdinalIgnoreCase));
        }

        var snapshot = Snapshot.Take(calendar);

        Apply(arguments, calendar, ownerId);

        var options = new CalendarChangeOptions { Actor = CalendarUser.GetId(user) };
        CalendarResult<Calendar> result;

        if (isNew)
        {
            CalendarCreation.EnsureCreatorCanManage(calendar, user);
            result = await calendarService.CreateAsync(calendar, options, cancellationToken);
        }
        else
        {
            result = await calendarService.UpdateAsync(calendar, options, cancellationToken);
        }

        if (!result.Succeeded)
        {
            snapshot.Restore(calendar);

            return (isNew ? "The calendar was not created: " : "The calendar was not saved: ") + CalendarToolSupport.Describe(result.Errors);
        }

        var saved = result.Value ?? calendar;
        var savedLevel = await accessService.GetAccessLevelAsync(user, saved, cancellationToken);

        return CalendarToolSupport.Json(new
        {
            created = isNew,
            calendar = await CalendarToolResults.DescribeCalendarAsync(services, saved, savedLevel),
        });
    }

    private static async Task<string> ValidateAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, Calendar calendar, bool isNew)
    {
        var name = CalendarToolSupport.ReadRaw(arguments, "name");

        if ((isNew && string.IsNullOrEmpty(name ?? calendar.Name)) || (name is not null && name.Length == 0))
        {
            return "The name is required.";
        }

        if (CalendarToolSupport.ReadRaw(arguments, "timeZoneId") is { Length: > 0 } timeZoneId && !CalendarTime.IsValidZone(CalendarTime.NormalizeZoneId(timeZoneId) ?? timeZoneId))
        {
            return $"'{timeZoneId}' is not a time zone. Use an IANA time zone such as America/New_York.";
        }

        if (Read(arguments, "visibility") is { } visibility && !Enum.TryParse<CalendarVisibility>(visibility, ignoreCase: true, out _))
        {
            return "The visibility must be Private, Shared or Public.";
        }

        if (CalendarToolSupport.Has(arguments, "enabled") && CalendarToolSupport.ReadBool(arguments, "enabled") is null)
        {
            return "enabled must be true or false.";
        }

        if (CalendarToolSupport.Has(arguments, "limitOverlaps") && CalendarToolSupport.ReadBool(arguments, "limitOverlaps") is null)
        {
            return "limitOverlaps must be true or false.";
        }

        if (CalendarToolSupport.Has(arguments, "capacity") && CalendarToolSupport.ReadInt(arguments, "capacity") is not >= 1)
        {
            return "The number of events that may overlap must be at least 1.";
        }

        var isResource = string.Equals(calendar.Type, CalendarsConstants.Types.Room, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(calendar.Type, CalendarsConstants.Types.Equipment, StringComparison.OrdinalIgnoreCase);
        var isOffice = string.Equals(calendar.Type, CalendarsConstants.Types.Office, StringComparison.OrdinalIgnoreCase);

        if (!isResource && _resourceFields.FirstOrDefault(field => CalendarToolSupport.Has(arguments, field)) is { } resourceField)
        {
            return $"{resourceField} applies only to Room and Equipment calendars.";
        }

        if (!isOffice && _officeFields.FirstOrDefault(field => CalendarToolSupport.Has(arguments, field)) is { } officeField)
        {
            return $"{officeField} applies only to Office calendars.";
        }

        if (CalendarToolSupport.Has(arguments, "seats") && CalendarToolSupport.ReadInt(arguments, "seats") is not >= 0)
        {
            return "The number of seats must be 0 or more.";
        }

        return await ValidateOwnerAsync(arguments, services, user, calendar, isNew);
    }

    private static async Task<string> ValidateOwnerAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, Calendar calendar, bool isNew)
    {
        var ownerId = Read(arguments, "ownerId");

        switch (calendar.OwnerKind)
        {
            case CalendarOwnerKind.Role:
                if (ownerId is null)
                {
                    return isNew ? "Give ownerId: the team (role) the calendar belongs to." : null;
                }

                var roleService = services.GetService<IRoleService>();
                IEnumerable<string> roles = roleService is null ? [] : await roleService.GetRoleNamesAsync();

                return roles.Any(role => string.Equals(role, ownerId, StringComparison.OrdinalIgnoreCase) && !string.Equals(role, "Anonymous", StringComparison.OrdinalIgnoreCase))
                    ? null
                    : $"There is no role '{ownerId}' a team calendar can belong to.";

            case CalendarOwnerKind.User:
                if (ownerId is null || string.Equals(ownerId, calendar.OwnerId, StringComparison.Ordinal))
                {
                    return null;
                }

                // As on the Owner card, only a calendar administrator chooses whose calendar it is.
                if (!await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(user, CalendarPermissions.ManageAllCalendars))
                {
                    return "Only a calendar administrator can choose whose calendar it is.";
                }

                var userLookup = services.GetService<CalendarUserLookup>();
                IReadOnlyList<CalendarUserItem> users = userLookup is null ? [] : await userLookup.GetAsync([ownerId]);

                return users.Count == 1 ? null : $"There is no user with the ID '{ownerId}'.";

            default:
                return ownerId is null ? null : $"{calendar.Type} calendars have no owner.";
        }
    }

    private static void Apply(AIFunctionArguments arguments, Calendar calendar, string ownerId)
    {
        if (CalendarToolSupport.ReadRaw(arguments, "name") is { Length: > 0 } name)
        {
            calendar.Name = name;
        }

        if (CalendarToolSupport.ReadRaw(arguments, "description") is { } description)
        {
            calendar.Description = description.Length == 0 ? null : description;
        }

        if (CalendarToolSupport.ReadRaw(arguments, "color") is { } color)
        {
            calendar.Color = color.Length == 0 ? null : color;
        }

        if (CalendarToolSupport.ReadRaw(arguments, "timeZoneId") is { } timeZoneId)
        {
            calendar.TimeZoneId = timeZoneId.Length == 0 ? null : CalendarTime.NormalizeZoneId(timeZoneId) ?? timeZoneId;
        }

        if (Read(arguments, "visibility") is { } visibility)
        {
            calendar.Visibility = Enum.Parse<CalendarVisibility>(visibility, ignoreCase: true);
        }

        if (CalendarToolSupport.ReadBool(arguments, "enabled") is { } enabled)
        {
            calendar.Enabled = enabled;
        }

        if (ownerId is not null)
        {
            calendar.OwnerId = ownerId;
        }

        if (CalendarToolSupport.ReadBool(arguments, "limitOverlaps") is { } limitOverlaps)
        {
            calendar.ConflictPolicy = limitOverlaps ? CalendarConflictPolicy.LimitConcurrent : CalendarConflictPolicy.AllowOverlaps;
        }

        if (CalendarToolSupport.ReadInt(arguments, "capacity") is { } capacity)
        {
            calendar.Capacity = Math.Max(1, capacity);
        }

        if (_resourceFields.Any(field => CalendarToolSupport.Has(arguments, field)))
        {
            var current = calendar.GetOrCreate<ResourceCalendarSettings>();

            calendar.Put(new ResourceCalendarSettings
            {
                Location = Text(arguments, "location", current.Location),
                Seats = Math.Max(0, CalendarToolSupport.ReadInt(arguments, "seats") ?? current.Seats),
                Amenities = Text(arguments, "amenities", current.Amenities),
            });
        }

        if (_officeFields.Any(field => CalendarToolSupport.Has(arguments, field)))
        {
            var current = calendar.GetOrCreate<OfficeCalendarSettings>();

            calendar.Put(new OfficeCalendarSettings
            {
                Address = Text(arguments, "address", current.Address),
                Contact = Text(arguments, "contact", current.Contact),
            });
        }
    }

    private static string Text(AIFunctionArguments arguments, string name, string current)
    {
        var value = CalendarToolSupport.ReadRaw(arguments, name);

        return value is null ? current : value.Length == 0 ? null : value;
    }

    // The fields this tool changes, so a save the calendar service refuses leaves the loaded calendar as it was.
    private sealed class Snapshot
    {
        private string _name;
        private string _description;
        private string _color;
        private string _timeZoneId;
        private CalendarVisibility _visibility;
        private bool _enabled;
        private string _ownerId;
        private CalendarConflictPolicy _conflictPolicy;
        private int _capacity;
        private List<CalendarShare> _shares;
        private ResourceCalendarSettings _resource;
        private OfficeCalendarSettings _office;

        public static Snapshot Take(Calendar calendar)
            => new()
            {
                _name = calendar.Name,
                _description = calendar.Description,
                _color = calendar.Color,
                _timeZoneId = calendar.TimeZoneId,
                _visibility = calendar.Visibility,
                _enabled = calendar.Enabled,
                _ownerId = calendar.OwnerId,
                _conflictPolicy = calendar.ConflictPolicy,
                _capacity = calendar.Capacity,
                _shares = calendar.Shares.ToList(),
                _resource = calendar.TryGet<ResourceCalendarSettings>(out var resource) ? resource : null,
                _office = calendar.TryGet<OfficeCalendarSettings>(out var office) ? office : null,
            };

        public void Restore(Calendar calendar)
        {
            calendar.Name = _name;
            calendar.Description = _description;
            calendar.Color = _color;
            calendar.TimeZoneId = _timeZoneId;
            calendar.Visibility = _visibility;
            calendar.Enabled = _enabled;
            calendar.OwnerId = _ownerId;
            calendar.ConflictPolicy = _conflictPolicy;
            calendar.Capacity = _capacity;
            calendar.Shares = _shares;

            if (_resource is not null)
            {
                calendar.Put(_resource);
            }

            if (_office is not null)
            {
                calendar.Put(_office);
            }
        }
    }
}
