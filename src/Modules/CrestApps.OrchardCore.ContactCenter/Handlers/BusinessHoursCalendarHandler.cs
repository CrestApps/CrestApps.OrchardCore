using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.Core.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Deployments;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Handlers;

internal sealed class BusinessHoursCalendarHandler : CatalogEntryHandlerBase<BusinessHoursCalendar>
{
    private readonly TimeProvider _timeProvider;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessHoursCalendarHandler"/> class.
    /// </summary>
    /// <param name="timeProvider">The time provider used to stamp audit times.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public BusinessHoursCalendarHandler(
        TimeProvider timeProvider,
        IStringLocalizer<BusinessHoursCalendarHandler> stringLocalizer)
    {
        _timeProvider = timeProvider;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override Task InitializingAsync(InitializingContext<BusinessHoursCalendar> context, CancellationToken cancellationToken = default)
    {
        ContactCenterDeploymentSerializer.Populate(context.Model, context.Data);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task InitializedAsync(InitializedContext<BusinessHoursCalendar> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _timeProvider.GetUtcNow().UtcDateTime;

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task UpdatingAsync(UpdatingContext<BusinessHoursCalendar> context, CancellationToken cancellationToken = default)
    {
        ContactCenterDeploymentSerializer.Populate(context.Model, context.Data);

        context.Model.ModifiedUtc = _timeProvider.GetUtcNow().UtcDateTime;

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
