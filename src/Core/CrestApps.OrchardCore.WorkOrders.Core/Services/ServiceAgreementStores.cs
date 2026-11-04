using System.ComponentModel.DataAnnotations;
using System.Globalization;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Calendars.Core.Recurrence;
using CrestApps.OrchardCore.WorkOrders.Core.Indexes;
using CrestApps.OrchardCore.WorkOrders.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.WorkOrders.Core.Services;

/// <summary>
/// Stores service agreements.
/// </summary>
public sealed class ServiceAgreementStore : DocumentCatalog<ServiceAgreement, ServiceAgreementIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceAgreementStore"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    public ServiceAgreementStore(ISession session)
        : base(session)
    {
        CollectionName = WorkOrdersConstants.CollectionName;
    }
}

/// <summary>
/// Manages service agreements.
/// </summary>
public sealed class ServiceAgreementManager : CatalogManager<ServiceAgreement>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceAgreementManager"/> class.
    /// </summary>
    /// <param name="store">The store.</param>
    /// <param name="handlers">The handlers.</param>
    /// <param name="logger">The logger.</param>
    public ServiceAgreementManager(ServiceAgreementStore store, IEnumerable<ICatalogEntryHandler<ServiceAgreement>> handlers, ILogger<CatalogManager<ServiceAgreement>> logger)
        : base(store, handlers, logger)
    {
    }
}

/// <summary>
/// Numbers, stamps and checks service agreements.
/// </summary>
internal sealed class ServiceAgreementHandler : CatalogEntryHandlerBase<ServiceAgreement>
{
    private readonly ISession _session;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceAgreementHandler"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ServiceAgreementHandler(ISession session, IClock clock, IStringLocalizer<ServiceAgreementHandler> stringLocalizer)
    {
        _session = session;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override Task InitializedAsync(InitializedContext<ServiceAgreement> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _clock.UtcNow;
        context.Model.StartDate = DateOnly.FromDateTime(_clock.UtcNow);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override async Task CreatingAsync(CreatingContext<ServiceAgreement> context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(context.Model.Number))
        {
            var numbers = await _session.QueryIndex<ServiceAgreementIndex>(index => index.Number.StartsWith(WorkOrdersConstants.AgreementNumberPrefix), collection: WorkOrdersConstants.CollectionName).ListAsync(cancellationToken);
            var last = numbers
                .Select(index => int.TryParse(index.Number.AsSpan(WorkOrdersConstants.AgreementNumberPrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : 0)
                .DefaultIfEmpty(0)
                .Max();

            context.Model.Number = WorkOrdersConstants.AgreementNumberPrefix + (last + 1).ToString("D5", CultureInfo.InvariantCulture);
        }
    }

    /// <inheritdoc/>
    public override Task ValidatingAsync(ValidatingContext<ServiceAgreement> context, CancellationToken cancellationToken = default)
    {
        var agreement = context.Model;

        if (string.IsNullOrWhiteSpace(agreement.Name))
        {
            context.Result.Fail(new ValidationResult(S["The name is required."], [nameof(ServiceAgreement.Name)]));
        }

        if (agreement.EndDate < agreement.StartDate)
        {
            context.Result.Fail(new ValidationResult(S["The agreement can't end before it starts."], [nameof(ServiceAgreement.EndDate)]));
        }

        if (agreement.Pricing == AgreementPricing.PerVisit && agreement.PricePerVisit <= 0)
        {
            context.Result.Fail(new ValidationResult(S["Enter the price of a visit."], [nameof(ServiceAgreement.PricePerVisit)]));
        }

        if (agreement.ExpiringNoticeDays is < 0 or > 365)
        {
            context.Result.Fail(new ValidationResult(S["The notice before the end is between 0 and 365 days."], [nameof(ServiceAgreement.ExpiringNoticeDays)]));
        }

        if (agreement.Status == ServiceAgreementStatus.Active && agreement.Schedules.Count == 0)
        {
            context.Result.Fail(new ValidationResult(S["An active agreement needs at least one schedule."], [nameof(ServiceAgreement.Schedules)]));
        }

        foreach (var schedule in agreement.Schedules)
        {
            var name = string.IsNullOrWhiteSpace(schedule.Name) ? S["a schedule"].Value : schedule.Name;

            if (string.IsNullOrEmpty(schedule.TypeId))
            {
                context.Result.Fail(new ValidationResult(S["Choose the work order type of {0}.", name], [nameof(ServiceAgreement.Schedules)]));
            }

            if (!RecurrenceRule.TryParse(schedule.Rule, out _, out var error))
            {
                context.Result.Fail(new ValidationResult(S["When {0} repeats isn't a valid rule: {1}", name, error], [nameof(ServiceAgreement.Schedules)]));
            }

            if (schedule.LeadDays is < 0 or > 365)
            {
                context.Result.Fail(new ValidationResult(S["{0} is made between 0 and 365 days ahead.", name], [nameof(ServiceAgreement.Schedules)]));
            }
        }

        return Task.CompletedTask;
    }
}
