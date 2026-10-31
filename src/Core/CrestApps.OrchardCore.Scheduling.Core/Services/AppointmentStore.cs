using CrestApps.OrchardCore.Scheduling.Core.Indexes;
using CrestApps.OrchardCore.Scheduling.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Scheduling.Core.Services;

/// <summary>
/// Stores appointments.
/// </summary>
public sealed class AppointmentStore : DocumentCatalog<Appointment, AppointmentIndex>
{
    /// <summary>
    /// The states of an appointment that takes its time.
    /// </summary>
    public static readonly AppointmentStatus[] ActiveStatuses = [AppointmentStatus.Held, AppointmentStatus.Requested, AppointmentStatus.Confirmed, AppointmentStatus.Completed];

    // A customer and a staff member changing the same appointment must not overwrite each other.
    protected override bool CheckConcurrency => true;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppointmentStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public AppointmentStore(ISession session)
        : base(session)
    {
        CollectionName = SchedulingConstants.CollectionName;
    }

    /// <summary>
    /// Lists the active appointments of some resources that overlap a range.
    /// </summary>
    /// <param name="resourceIds">The resources.</param>
    /// <param name="fromUtc">The start of the range.</param>
    /// <param name="toUtc">The end of the range.</param>
    /// <returns>The appointments.</returns>
    public async Task<IReadOnlyList<Appointment>> ListForResourcesAsync(IEnumerable<string> resourceIds, DateTime fromUtc, DateTime toUtc)
    {
        var ids = resourceIds.Where(id => !string.IsNullOrEmpty(id)).Distinct().ToArray();

        if (ids.Length == 0)
        {
            return [];
        }

        var statuses = ActiveStatuses;
        var appointments = await Session.Query<Appointment, AppointmentResourceIndex>(
                index => index.ResourceId.IsIn(ids) && index.StartUtc < toUtc && index.EndUtc > fromUtc && index.Status.IsIn(statuses),
                collection: CollectionName)
            .Take(2000)
            .ListAsync();

        return await LoadedAsync(appointments.DistinctBy(appointment => appointment.ItemId));
    }

    /// <summary>
    /// Counts the appointments of some resources since a moment, for round-robin assignment.
    /// </summary>
    /// <param name="resourceId">The resource.</param>
    /// <param name="sinceUtc">The moment.</param>
    /// <returns>How many.</returns>
    public Task<int> CountForResourceAsync(string resourceId, DateTime sinceUtc)
    {
        var statuses = ActiveStatuses;

        return Session.QueryIndex<AppointmentResourceIndex>(index => index.ResourceId == resourceId && index.StartUtc >= sinceUtc && index.Status.IsIn(statuses), collection: CollectionName).CountAsync();
    }

    /// <summary>
    /// Lists holds that ran out.
    /// </summary>
    /// <param name="nowUtc">The current time.</param>
    /// <returns>The appointments.</returns>
    public async Task<IReadOnlyList<Appointment>> ListExpiredHoldsAsync(DateTime nowUtc)
    {
        return await LoadedAsync(await Session.Query<Appointment, AppointmentIndex>(index => index.Status == AppointmentStatus.Held && index.HoldExpiresUtc < nowUtc, collection: CollectionName).Take(200).ListAsync());
    }

    /// <summary>
    /// Lists a page of appointments.
    /// </summary>
    /// <param name="status">The state to match, or <see langword="null"/>.</param>
    /// <param name="search">Text the customer's name or email must contain.</param>
    /// <param name="fromUtc">The earliest start to match, or <see langword="null"/>.</param>
    /// <param name="skip">How many to skip.</param>
    /// <param name="take">How many to return.</param>
    /// <returns>How many match, and the page.</returns>
    public async Task<(int Count, IReadOnlyList<Appointment> Appointments)> PageAsync(AppointmentStatus? status, string search, DateTime? fromUtc, int skip, int take)
    {
        var query = Session.Query<Appointment, AppointmentIndex>(collection: CollectionName);

        if (status.HasValue)
        {
            var value = status.Value;

            query = query.Where(index => index.Status == value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var text = search.Trim();
            var lower = text.ToLowerInvariant();

            query = query.Where(index => index.CustomerName.Contains(text) || index.CustomerEmail.Contains(lower) || index.CustomerPhone.Contains(text));
        }

        if (fromUtc.HasValue)
        {
            var from = fromUtc.Value;

            query = query.Where(index => index.StartUtc >= from);
        }

        var count = await query.CountAsync();
        var page = await query.OrderBy(index => index.StartUtc).Skip(skip).Take(take).ListAsync();

        return (count, await LoadedAsync(page));
    }

    /// <summary>
    /// Lists a customer's upcoming appointments by email, phone or user id.
    /// </summary>
    /// <param name="email">The customer's email.</param>
    /// <param name="phone">The customer's phone number.</param>
    /// <param name="userId">The customer's user id.</param>
    /// <param name="fromUtc">The earliest start.</param>
    /// <returns>The appointments.</returns>
    public async Task<IReadOnlyList<Appointment>> ListForCustomerAsync(string email, string phone, string userId, DateTime fromUtc)
    {
        var lower = email?.Trim().ToLowerInvariant();
        var statuses = ActiveStatuses;

        if (string.IsNullOrEmpty(lower) && string.IsNullOrEmpty(phone) && string.IsNullOrEmpty(userId))
        {
            return [];
        }

        var appointments = await Session.Query<Appointment, AppointmentIndex>(
                index => index.StartUtc >= fromUtc && index.Status.IsIn(statuses) &&
                    ((lower != null && index.CustomerEmail == lower) || (phone != null && index.CustomerPhone == phone) || (userId != null && index.UserId == userId)),
                collection: CollectionName)
            .OrderBy(index => index.StartUtc)
            .Take(50)
            .ListAsync();

        return await LoadedAsync(appointments);
    }
}
