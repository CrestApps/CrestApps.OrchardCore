using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.BusinessHours.Models;
using CrestApps.OrchardCore.Calendars;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.BusinessHours.Core.Handlers;

/// <summary>
/// Tidies and validates business hours schedules, and stamps when they were created and changed.
/// </summary>
public sealed class BusinessHoursScheduleHandler : CatalogEntryHandlerBase<BusinessHoursSchedule>
{
    private const int MinutesPerDay = 1440;

    private readonly INamedCatalog<BusinessHoursSchedule> _store;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessHoursScheduleHandler"/> class.
    /// </summary>
    /// <param name="store">The schedule store.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public BusinessHoursScheduleHandler(
        INamedCatalog<BusinessHoursSchedule> store,
        IClock clock,
        IStringLocalizer<BusinessHoursScheduleHandler> stringLocalizer)
    {
        _store = store;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override Task InitializingAsync(InitializingContext<BusinessHoursSchedule> context, CancellationToken cancellationToken = default)
    {
        // A recipe creates the entry from its JSON.
        CatalogDeploymentSerializer.Populate(context.Model, context.Data);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task InitializedAsync(InitializedContext<BusinessHoursSchedule> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task CreatingAsync(CreatingContext<BusinessHoursSchedule> context, CancellationToken cancellationToken = default)
    {
        Tidy(context.Model);

        if (context.Model.CreatedUtc == default)
        {
            context.Model.CreatedUtc = _clock.UtcNow;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task UpdatingAsync(UpdatingContext<BusinessHoursSchedule> context, CancellationToken cancellationToken = default)
    {
        // A recipe updates the entry from its JSON; the editor has already changed it.
        CatalogDeploymentSerializer.Populate(context.Model, context.Data);
        Tidy(context.Model);
        context.Model.ModifiedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override async Task ValidatingAsync(ValidatingContext<BusinessHoursSchedule> context, CancellationToken cancellationToken = default)
    {
        var schedule = context.Model;

        Tidy(schedule);

        if (string.IsNullOrEmpty(schedule.Name))
        {
            context.Result.Fail(new ValidationResult(S["The name is required."], [nameof(BusinessHoursSchedule.Name)]));
        }
        else if (await _store.FindByNameAsync(schedule.Name, cancellationToken) is { } existing
            && !string.Equals(existing.ItemId, schedule.ItemId, StringComparison.Ordinal))
        {
            context.Result.Fail(new ValidationResult(S["Another schedule has this name."], [nameof(BusinessHoursSchedule.Name)]));
        }

        if (!string.IsNullOrEmpty(schedule.Hours.TimeZoneId) && !CalendarTimeZones.IsValid(schedule.Hours.TimeZoneId))
        {
            context.Result.Fail(new ValidationResult(S["The time zone is not known."], [nameof(HoursSchedule.TimeZoneId)]));
        }

        if (schedule.Hours.Windows.Any(window => !IsMinute(window.OpenMinute) || !IsMinute(window.CloseMinute)))
        {
            context.Result.Fail(new ValidationResult(S["Each opening and closing time must be a time of day."], [nameof(HoursSchedule.Windows)]));
        }

        if (schedule.Hours.DateExceptions.Any(exception => !exception.Closed && (!IsMinute(exception.OpenMinute) || !IsMinute(exception.CloseMinute))))
        {
            context.Result.Fail(new ValidationResult(S["Each special day's hours must be times of day."], [nameof(HoursSchedule.DateExceptions)]));
        }
    }

    private static bool IsMinute(int minute)
        => minute is >= 0 and <= MinutesPerDay;

    private static void Tidy(BusinessHoursSchedule schedule)
    {
        schedule.Name = schedule.Name?.Trim();
        schedule.Description = string.IsNullOrWhiteSpace(schedule.Description) ? null : schedule.Description.Trim();
        schedule.Hours ??= new HoursSchedule();
        schedule.Hours.TimeZoneId = string.IsNullOrWhiteSpace(schedule.Hours.TimeZoneId) ? null : schedule.Hours.TimeZoneId.Trim();
        schedule.Hours.Windows = [.. (schedule.Hours.Windows ?? [])
            .Where(window => window is not null)
            .OrderBy(window => window.Day == DayOfWeek.Sunday ? 7 : (int)window.Day)
            .ThenBy(window => window.OpenMinute)];
        schedule.Hours.DateExceptions = [.. (schedule.Hours.DateExceptions ?? [])
            .Where(exception => exception is not null)
            .GroupBy(exception => exception.Date)
            .Select(group => group.Last())
            .OrderBy(exception => exception.Date)];
    }
}
