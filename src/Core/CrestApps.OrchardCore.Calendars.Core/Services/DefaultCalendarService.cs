using System.Text.RegularExpressions;
using CrestApps.Core;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Calendars.Core.Services;

/// <summary>
/// Creates, changes, deletes and finds calendars, validating them against their type and running the <see cref="ICalendarHandler"/>s.
/// </summary>
public sealed partial class DefaultCalendarService : ICalendarService
{
    private const int DeleteBatchSize = 500;

    private readonly ICalendarStore _calendarStore;
    private readonly ICalendarEventStore _eventStore;
    private readonly ICalendarTypeManager _typeManager;
    private readonly IEnumerable<ICalendarHandler> _handlers;
    private readonly ISiteService _siteService;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultCalendarService"/> class.
    /// </summary>
    /// <param name="calendarStore">The calendar store.</param>
    /// <param name="eventStore">The event store.</param>
    /// <param name="typeManager">The calendar type manager.</param>
    /// <param name="handlers">The calendar handlers.</param>
    /// <param name="siteService">The site service, for the site's time zone.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DefaultCalendarService(
        ICalendarStore calendarStore,
        ICalendarEventStore eventStore,
        ICalendarTypeManager typeManager,
        IEnumerable<ICalendarHandler> handlers,
        ISiteService siteService,
        IClock clock,
        ILogger<DefaultCalendarService> logger,
        IStringLocalizer<DefaultCalendarService> stringLocalizer)
    {
        _calendarStore = calendarStore;
        _eventStore = eventStore;
        _typeManager = typeManager;
        _handlers = handlers;
        _siteService = siteService;
        _clock = clock;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public async Task<Calendar> NewAsync(string type)
    {
        var descriptor = _typeManager.FindType(type);

        if (descriptor is null)
        {
            return null;
        }

        var site = await _siteService.GetSiteSettingsAsync();

        return new Calendar
        {
            ItemId = UniqueId.GenerateId(),
            Type = descriptor.Name,
            OwnerKind = descriptor.OwnerKind,
            Color = descriptor.DefaultColor,
            Visibility = descriptor.DefaultVisibility,
            ConflictPolicy = descriptor.DefaultConflictPolicy,
            Capacity = descriptor.DefaultCapacity,
            TimeZoneId = CalendarTime.NormalizeZoneId(site.TimeZoneId) ?? "UTC",
            Enabled = true,
        };
    }

    /// <inheritdoc />
    public ValueTask<Calendar> FindByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(id))
        {
            return ValueTask.FromResult<Calendar>(null);
        }

        return _calendarStore.FindByIdAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyCollection<Calendar>> GetAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        var list = ids.Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();

        if (list.Count == 0)
        {
            return ValueTask.FromResult<IReadOnlyCollection<Calendar>>([]);
        }

        return _calendarStore.GetAsync(list, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Calendar> FindBySourceAsync(string source, string sourceId, CancellationToken cancellationToken = default)
    {
        return _calendarStore.FindBySourceAsync(source, sourceId, cancellationToken);
    }

    /// <inheritdoc />
    public Task<(int Count, IReadOnlyList<Calendar> Calendars)> PageAsync(CalendarListQuery query, int skip, int take, CancellationToken cancellationToken = default)
    {
        return _calendarStore.PageAsync(query ?? new CalendarListQuery(), skip, take, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Calendar>> ListAsync(CalendarListQuery query, CancellationToken cancellationToken = default)
    {
        return _calendarStore.ListAsync(query ?? new CalendarListQuery(), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<CalendarResult<Calendar>> CreateAsync(Calendar calendar, CalendarChangeOptions options = null)
    {
        ArgumentNullException.ThrowIfNull(calendar);

        options ??= new CalendarChangeOptions();

        if (string.IsNullOrEmpty(calendar.ItemId))
        {
            calendar.ItemId = UniqueId.GenerateId();
        }

        var type = _typeManager.FindType(calendar.Type);

        Normalize(calendar, type);

        var errors = await ValidateAsync(calendar, type, options, isNew: true);

        if (errors.Count > 0)
        {
            return CalendarResult<Calendar>.Failed(calendar, errors);
        }

        var now = _clock.UtcNow;

        calendar.CreatedUtc = now;
        calendar.ModifiedUtc = now;
        calendar.CreatedBy ??= options.Actor;

        await _calendarStore.CreateAsync(calendar);

        var context = new CalendarContext(calendar, type, options);

        await _handlers.InvokeAsync((handler, context) => handler.CreatedAsync(context), context, _logger);

        return CalendarResult<Calendar>.Success(calendar);
    }

    /// <inheritdoc />
    public async Task<CalendarResult<Calendar>> UpdateAsync(Calendar calendar, CalendarChangeOptions options = null)
    {
        ArgumentNullException.ThrowIfNull(calendar);

        options ??= new CalendarChangeOptions();

        var type = _typeManager.FindType(calendar.Type);

        Normalize(calendar, type);

        var errors = await ValidateAsync(calendar, type, options, isNew: false);

        if (errors.Count > 0)
        {
            return CalendarResult<Calendar>.Failed(calendar, errors);
        }

        calendar.ModifiedUtc = _clock.UtcNow;

        await _calendarStore.UpdateAsync(calendar);

        var context = new CalendarContext(calendar, type, options);

        await _handlers.InvokeAsync((handler, context) => handler.UpdatedAsync(context), context, _logger);

        return CalendarResult<Calendar>.Success(calendar);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Calendar calendar, CalendarChangeOptions options = null)
    {
        ArgumentNullException.ThrowIfNull(calendar);

        var context = new CalendarContext(calendar, _typeManager.FindType(calendar.Type), options);

        await _handlers.InvokeAsync((handler, context) => handler.DeletingAsync(context), context, _logger);

        // Collect every event first; deleting while paging would shift the pages under the query.
        var events = new List<CalendarEvent>();

        for (var skip = 0; ; skip += DeleteBatchSize)
        {
            var batch = await _eventStore.ListByCalendarAsync(calendar.ItemId, skip, DeleteBatchSize);

            events.AddRange(batch);

            if (batch.Count < DeleteBatchSize)
            {
                break;
            }
        }

        foreach (var calendarEvent in events)
        {
            await _eventStore.DeleteAsync(calendarEvent);
        }

        await _calendarStore.DeleteAsync(calendar);
    }

    /// <inheritdoc />
    public async Task<Calendar> GetOrCreatePersonalCalendarAsync(string userId, string userName)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        var query = new CalendarListQuery
        {
            OwnerId = userId,
            Limit = 1,
        };

        query.Types.Add(CalendarsConstants.Types.Personal);

        var existing = await _calendarStore.ListAsync(query);

        if (existing.Count > 0)
        {
            return existing[0];
        }

        var calendar = await NewAsync(CalendarsConstants.Types.Personal);

        if (calendar is null)
        {
            return null;
        }

        calendar.OwnerId = userId;
        calendar.Name = string.IsNullOrWhiteSpace(userName) ? S["My calendar"].Value : userName;

        var result = await CreateAsync(calendar, new CalendarChangeOptions { Actor = userId });

        return result.Succeeded ? result.Value : null;
    }

    private static void Normalize(Calendar calendar, CalendarTypeDescriptor type)
    {
        calendar.Name = calendar.Name?.Trim();
        calendar.Shares ??= [];
        calendar.TimeZoneId = string.IsNullOrWhiteSpace(calendar.TimeZoneId) ? null : CalendarTime.NormalizeZoneId(calendar.TimeZoneId) ?? calendar.TimeZoneId.Trim();

        if (type is not null)
        {
            calendar.OwnerKind = type.OwnerKind;
        }

        if (calendar.OwnerKind is CalendarOwnerKind.None or CalendarOwnerKind.Resource)
        {
            calendar.OwnerId = null;
        }

        if (calendar.Capacity < 1)
        {
            calendar.Capacity = 1;
        }

        var shares = calendar.Shares
            .Where(share => !string.IsNullOrWhiteSpace(share.PrincipalId) && share.Access > CalendarAccessLevel.None)
            .GroupBy(share => (share.PrincipalKind, share.PrincipalId.Trim()))
            .Select(group => group.OrderByDescending(share => share.Access).First())
            .ToList();

        calendar.Shares = shares;
    }

    private async Task<List<CalendarError>> ValidateAsync(Calendar calendar, CalendarTypeDescriptor type, CalendarChangeOptions options, bool isNew)
    {
        var errors = new List<CalendarError>();

        if (string.IsNullOrWhiteSpace(calendar.Name))
        {
            errors.Add(new CalendarError(nameof(Calendar.Name), S["The name is required."]));
        }
        else if (calendar.Name.Length > 255)
        {
            errors.Add(new CalendarError(nameof(Calendar.Name), S["The name can't be longer than 255 characters."]));
        }

        if (type is null)
        {
            errors.Add(new CalendarError(nameof(Calendar.Type), S["The calendar type '{0}' is not available. Enable the feature that adds it.", calendar.Type ?? string.Empty]));
        }
        else if (calendar.OwnerKind is CalendarOwnerKind.User or CalendarOwnerKind.Role && string.IsNullOrWhiteSpace(calendar.OwnerId))
        {
            errors.Add(new CalendarError(nameof(Calendar.OwnerId), calendar.OwnerKind == CalendarOwnerKind.User ? S["Choose the user the calendar belongs to."] : S["Choose the team (role) the calendar belongs to."]));
        }
        else if (type.OnePerOwner && !string.IsNullOrEmpty(calendar.OwnerId))
        {
            var query = new CalendarListQuery
            {
                OwnerId = calendar.OwnerId,
                Limit = 2,
            };

            query.Types.Add(type.Name);

            var existing = await _calendarStore.ListAsync(query);

            if (existing.Any(other => other.ItemId != calendar.ItemId))
            {
                errors.Add(new CalendarError(nameof(Calendar.OwnerId), S["The owner already has a {0} calendar.", type.Name]));
            }
        }

        if (!string.IsNullOrEmpty(calendar.TimeZoneId) && !CalendarTime.IsValidZone(calendar.TimeZoneId))
        {
            errors.Add(new CalendarError(nameof(Calendar.TimeZoneId), S["'{0}' is not a time zone.", calendar.TimeZoneId]));
        }

        if (!string.IsNullOrEmpty(calendar.Color) && !ColorPattern().IsMatch(calendar.Color))
        {
            errors.Add(new CalendarError(nameof(Calendar.Color), S["The color must be a hex color such as #1971c2."]));
        }

        var context = new CalendarValidatingContext(calendar, type, options, isNew);

        await _handlers.InvokeAsync((handler, context) => handler.ValidatingAsync(context), context, _logger);

        errors.AddRange(context.Errors);

        return errors;
    }

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex ColorPattern();
}
