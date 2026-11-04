using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Workforce.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Workforce.Core.Handlers;

/// <summary>
/// Stamps and checks time-off allotments: a name, a team and a number for each weekday.
/// </summary>
internal sealed class TimeOffAllotmentCatalogHandler : CatalogEntryHandlerBase<TimeOffAllotment>
{
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public TimeOffAllotmentCatalogHandler(IClock clock, IStringLocalizer<TimeOffAllotmentCatalogHandler> stringLocalizer)
    {
        _clock = clock;
        S = stringLocalizer;
    }

    public override Task InitializedAsync(InitializedContext<TimeOffAllotment> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    public override Task UpdatingAsync(UpdatingContext<TimeOffAllotment> context, CancellationToken cancellationToken = default)
    {
        context.Model.ModifiedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    public override Task ValidatingAsync(ValidatingContext<TimeOffAllotment> context, CancellationToken cancellationToken = default)
    {
        var item = context.Model;

        item.Name = item.Name?.Trim();

        if (string.IsNullOrEmpty(item.Name))
        {
            context.Result.Fail(new ValidationResult(S["The name is required."], [nameof(TimeOffAllotment.Name)]));
        }

        if (string.IsNullOrEmpty(item.TeamId))
        {
            context.Result.Fail(new ValidationResult(S["Choose the team."], [nameof(TimeOffAllotment.TeamId)]));
        }

        if (item.WeekdaySlots.Count is not (0 or 7) || item.WeekdaySlots.Any(slots => slots < 0) || item.Dates.Any(date => date.Slots < 0))
        {
            context.Result.Fail(new ValidationResult(S["Give a number of people, 0 or more, for each day of the week."], [nameof(TimeOffAllotment.WeekdaySlots)]));
        }

        return Task.CompletedTask;
    }
}
