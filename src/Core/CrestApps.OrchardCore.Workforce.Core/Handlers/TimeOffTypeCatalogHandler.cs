using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Workforce.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Workforce.Core.Handlers;

/// <summary>
/// Stamps and checks time-off types: a name and a day length of up to a day.
/// </summary>
internal sealed class TimeOffTypeCatalogHandler : CatalogEntryHandlerBase<TimeOffType>
{
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public TimeOffTypeCatalogHandler(IClock clock, IStringLocalizer<TimeOffTypeCatalogHandler> stringLocalizer)
    {
        _clock = clock;
        S = stringLocalizer;
    }

    public override Task InitializedAsync(InitializedContext<TimeOffType> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    public override Task UpdatingAsync(UpdatingContext<TimeOffType> context, CancellationToken cancellationToken = default)
    {
        context.Model.ModifiedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    public override Task ValidatingAsync(ValidatingContext<TimeOffType> context, CancellationToken cancellationToken = default)
    {
        var item = context.Model;

        item.Name = item.Name?.Trim();

        if (string.IsNullOrEmpty(item.Name))
        {
            context.Result.Fail(new ValidationResult(S["The name is required."], [nameof(TimeOffType.Name)]));
        }

        if (item.HoursPerDay is <= 0 or > 24)
        {
            context.Result.Fail(new ValidationResult(S["A day lasts more than 0 and up to 24 hours."], [nameof(TimeOffType.HoursPerDay)]));
        }

        return Task.CompletedTask;
    }
}
