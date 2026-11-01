using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Workforce.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Workforce.Core.Handlers;

/// <summary>
/// Stamps and checks kiosks: a name.
/// </summary>
internal sealed class TimeClockKioskCatalogHandler : CatalogEntryHandlerBase<TimeClockKiosk>
{
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public TimeClockKioskCatalogHandler(IClock clock, IStringLocalizer<TimeClockKioskCatalogHandler> stringLocalizer)
    {
        _clock = clock;
        S = stringLocalizer;
    }

    public override Task InitializedAsync(InitializedContext<TimeClockKiosk> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    public override Task UpdatingAsync(UpdatingContext<TimeClockKiosk> context, CancellationToken cancellationToken = default)
    {
        context.Model.ModifiedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    public override Task ValidatingAsync(ValidatingContext<TimeClockKiosk> context, CancellationToken cancellationToken = default)
    {
        var item = context.Model;

        item.Name = item.Name?.Trim();

        if (string.IsNullOrEmpty(item.Name))
        {
            context.Result.Fail(new ValidationResult(S["The name is required."], [nameof(TimeClockKiosk.Name)]));
        }

        return Task.CompletedTask;
    }
}
