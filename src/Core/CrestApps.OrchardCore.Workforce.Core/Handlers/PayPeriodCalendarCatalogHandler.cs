using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Workforce.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Workforce.Core.Handlers;

/// <summary>
/// Stamps and checks pay period calendars: a name.
/// </summary>
internal sealed class PayPeriodCalendarCatalogHandler : CatalogEntryHandlerBase<PayPeriodCalendar>
{
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public PayPeriodCalendarCatalogHandler(IClock clock, IStringLocalizer<PayPeriodCalendarCatalogHandler> stringLocalizer)
    {
        _clock = clock;
        S = stringLocalizer;
    }

    public override Task InitializedAsync(InitializedContext<PayPeriodCalendar> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    public override Task UpdatingAsync(UpdatingContext<PayPeriodCalendar> context, CancellationToken cancellationToken = default)
    {
        context.Model.ModifiedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    public override Task ValidatingAsync(ValidatingContext<PayPeriodCalendar> context, CancellationToken cancellationToken = default)
    {
        var item = context.Model;

        item.Name = item.Name?.Trim();

        if (string.IsNullOrEmpty(item.Name))
        {
            context.Result.Fail(new ValidationResult(S["The name is required."], [nameof(PayPeriodCalendar.Name)]));
        }

        return Task.CompletedTask;
    }
}
