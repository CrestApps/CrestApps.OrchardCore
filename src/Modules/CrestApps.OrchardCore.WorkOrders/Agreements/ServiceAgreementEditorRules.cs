using CrestApps.Core;
using CrestApps.OrchardCore.Agreements.Core.Services;
using CrestApps.OrchardCore.Agreements.Models;
using CrestApps.OrchardCore.WorkOrders.Agreements.ViewModels;
using CrestApps.OrchardCore.WorkOrders.Core.Indexes;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Integrations;
using CrestApps.OrchardCore.WorkOrders.Models;
using NodaTime;
using YesSql;

namespace CrestApps.OrchardCore.WorkOrders.Agreements;

/// <summary>
/// The fields of a service agreement as the AI tools read and write them: the agreement's own fields and hours, which
/// the Agreements editor keeps, and its field-service terms, which the work orders' part of that editor keeps.
/// </summary>
internal sealed class ServiceAgreementForm
{
    /// <summary>
    /// Gets or sets the agreement's general fields: name, status, account, period, time zone, notice and notes.
    /// </summary>
    public AgreementGeneralFields General { get; set; } = new();

    /// <summary>
    /// Gets or sets how the agreement covers hours.
    /// </summary>
    public AgreementHoursKind HoursKind { get; set; }

    /// <summary>
    /// Gets or sets the hours of the block, or of each month's allowance.
    /// </summary>
    public decimal Hours { get; set; }

    /// <summary>
    /// Gets or sets what each entry of time is rounded up to, in minutes.
    /// </summary>
    public int RoundingMinutes { get; set; }

    /// <summary>
    /// Gets or sets the field-service terms, as the work orders' part of the editor posts them.
    /// </summary>
    public WorkOrderAgreementTermsViewModel Terms { get; set; } = new();
}

/// <summary>
/// What the service agreement AI tools and pages share: how an agreement fills the tools' fields and how the given
/// fields are applied to it, through the same rules as the Agreements editor (<see cref="AgreementEditorRules"/>) and its
/// work orders' part (<see cref="WorkOrderAgreementTermsRules"/>); the next visits of a schedule; and the work orders an
/// agreement made. A service agreement is a shared agreement (the Agreements feature) with field-service terms
/// (<see cref="WorkOrderAgreementTerms"/>).
/// </summary>
internal static class ServiceAgreementEditorRules
{
    /// <summary>
    /// Gives the work orders an agreement made, newest first.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="agreementId">The agreement.</param>
    /// <param name="take">How many to give at most.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The work orders.</returns>
    public static async Task<IReadOnlyList<WorkOrder>> WorkOrdersAsync(ISession session, string agreementId, int take, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrEmpty(agreementId);

        var prefix = agreementId + ":";
        var source = nameof(WorkOrderSource.Agreement);
        var workOrders = await session.Query<WorkOrder, WorkOrderIndex>(index => index.Source == source && index.SourceId.StartsWith(prefix), collection: WorkOrdersConstants.CollectionName)
            .OrderByDescending(index => index.CreatedUtc)
            .Take(take)
            .ListAsync(cancellationToken);

        return workOrders.ToList();
    }

    /// <summary>
    /// Gives the next days a schedule has a visit on, from today in the agreement's time zone or its start, whichever
    /// is later, within five years, as the agreement's visits page does.
    /// </summary>
    /// <param name="agreement">The agreement.</param>
    /// <param name="schedule">The schedule.</param>
    /// <param name="nowUtc">The time now.</param>
    /// <param name="count">How many days to give at most.</param>
    /// <returns>The days, in order.</returns>
    public static IReadOnlyList<DateOnly> NextVisits(Agreement agreement, AgreementSchedule schedule, DateTime nowUtc, int count)
    {
        ArgumentNullException.ThrowIfNull(agreement);
        ArgumentNullException.ThrowIfNull(schedule);

        var zone = ServiceAgreementScheduler.ZoneOf(agreement.TimeZoneId);
        var today = Instant.FromDateTimeUtc(DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc)).InZone(zone).Date;
        var from = LocalDate.Max(today, LocalDate.FromDateOnly(agreement.StartDate));

        return [.. ServiceAgreementScheduler.Occurrences(agreement, schedule, zone, from, from.PlusYears(5)).Take(count).Select(date => date.ToDateOnly())];
    }

    /// <summary>
    /// Gives an agreement's field-service terms, or empty ones when it has none.
    /// </summary>
    /// <param name="agreement">The agreement.</param>
    /// <returns>The terms.</returns>
    public static WorkOrderAgreementTerms TermsOf(Agreement agreement)
    {
        ArgumentNullException.ThrowIfNull(agreement);

        return agreement.TryGet<WorkOrderAgreementTerms>(out var terms) && terms is not null ? terms : new WorkOrderAgreementTerms();
    }

    /// <summary>
    /// Fills the tools' fields from an agreement, its hours and its terms, as the editors fill theirs. Every repeat rule
    /// is given as a custom rule; the editor's presets are such rules too.
    /// </summary>
    /// <param name="agreement">The agreement.</param>
    /// <returns>The fields.</returns>
    public static ServiceAgreementForm Fill(Agreement agreement)
    {
        ArgumentNullException.ThrowIfNull(agreement);

        var terms = TermsOf(agreement);
        var hours = agreement.Hours ?? new AgreementHours();
        var address = terms.ServiceAddress;

        return new ServiceAgreementForm
        {
            General = AgreementGeneralFields.From(agreement),
            HoursKind = hours.Kind,
            Hours = hours.Hours,
            RoundingMinutes = hours.RoundingMinutes,
            Terms = new WorkOrderAgreementTermsViewModel
            {
                CustomerId = terms.CustomerId,
                ContactName = terms.Contact?.Name,
                ContactPhone = terms.Contact?.Phone,
                ContactEmail = terms.Contact?.Email,
                AddressLine1 = address?.AddressLine1,
                City = address?.City,
                Region = address?.Region,
                PostalCode = address?.PostalCode,
                Country = address?.Country,
                AccessNotes = terms.AccessNotes,
                AssetIds = [.. terms.AssetIds],
                Pricing = terms.Pricing,
                PricePerVisit = terms.PricePerVisit,
                Schedules = [.. terms.Schedules.Select(schedule => new ScheduleEditModel
                {
                    Id = schedule.Id,
                    Name = schedule.Name,
                    TypeId = schedule.TypeId,
                    Rule = string.Empty,
                    CustomRule = schedule.Rule,
                    LeadDays = schedule.LeadDays,
                    AssetIds = [.. schedule.AssetIds],
                })],
            },
        };
    }

    /// <summary>
    /// Applies the tools' fields to an agreement in the order the editor's drivers apply a post, with their rules: the
    /// general fields (<see cref="AgreementEditorRules.ApplyGeneralFields"/>), the hours
    /// (<see cref="AgreementEditorRules.ApplyHours"/>), then the field-service terms
    /// (<see cref="WorkOrderAgreementTermsRules.ApplyAsync"/>).
    /// </summary>
    /// <param name="agreement">The agreement.</param>
    /// <param name="form">The fields.</param>
    /// <param name="customers">The customers, accounts and contacts.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that completes when the fields are applied.</returns>
    public static async Task ApplyAsync(Agreement agreement, ServiceAgreementForm form, WorkOrderCustomers customers, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agreement);
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(customers);

        AgreementEditorRules.ApplyGeneralFields(agreement, form.General ?? new AgreementGeneralFields());
        AgreementEditorRules.ApplyHours(agreement, form.HoursKind, form.Hours, form.RoundingMinutes);

        await WorkOrderAgreementTermsRules.ApplyAsync(agreement, form.Terms, customers, cancellationToken);
    }
}
