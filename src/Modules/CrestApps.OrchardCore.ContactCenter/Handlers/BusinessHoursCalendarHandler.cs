using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Handlers;

internal sealed class BusinessHoursCalendarHandler : CatalogEntryHandlerBase<BusinessHoursCalendar>
{
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessHoursCalendarHandler"/> class.
    /// </summary>
    /// <param name="clock">The clock used to stamp audit times.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public BusinessHoursCalendarHandler(
        IClock clock,
        IStringLocalizer<BusinessHoursCalendarHandler> stringLocalizer)
    {
        _clock = clock;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override Task InitializedAsync(InitializedContext<BusinessHoursCalendar> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task UpdatingAsync(UpdatingContext<BusinessHoursCalendar> context, CancellationToken cancellationToken = default)
    {
        context.Model.ModifiedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task ValidatingAsync(ValidatingContext<BusinessHoursCalendar> context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(context.Model.Name))
        {
            context.Result.Fail(new ValidationResult(S["Name is required."], [nameof(BusinessHoursCalendar.Name)]));
        }

        return Task.CompletedTask;
    }
}
