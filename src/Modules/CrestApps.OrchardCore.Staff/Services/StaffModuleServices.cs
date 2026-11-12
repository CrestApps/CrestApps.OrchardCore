using System.Globalization;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Geo.Models;
using CrestApps.OrchardCore.Geo.Services;
using CrestApps.OrchardCore.PersonalData;
using CrestApps.OrchardCore.Staff.Core.Services;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Staff.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Staff.Services;

/// <summary>
/// Exports and anonymizes the staff record of a person.
/// </summary>
internal sealed class StaffPersonalDataContributor : IPersonalDataContributor
{
    public const string Kind = "StaffMember";

    private readonly StaffMemberStore _store;

    internal readonly IStringLocalizer S;

    public StaffPersonalDataContributor(
        StaffMemberStore store,
        IStringLocalizer<StaffPersonalDataContributor> stringLocalizer)
    {
        _store = store;
        S = stringLocalizer;
    }

    public async Task IdentifyAsync(PersonalDataSubject subject, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subject);

        // Only the name, for scrubbing free text: an e-mail or phone on a record may be shared by other people.
        foreach (var staff in await FindAsync(subject, cancellationToken))
        {
            subject.AddName(staff.DisplayName);
        }
    }

    public async Task ExportAsync(PersonalDataExportContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var staff in await FindAsync(context.Subject, cancellationToken))
        {
            var record = context.Add(Kind, staff.ItemId, staff.DisplayName, staff.CreatedUtc);

            record.Fields[nameof(StaffMember.DisplayName)] = staff.DisplayName;
            record.Fields[nameof(StaffMember.Email)] = staff.Email;
            record.Fields[nameof(StaffMember.Phone)] = staff.Phone;
            record.Fields[nameof(StaffMember.EmployeeNumber)] = staff.EmployeeNumber;
            record.Fields[nameof(StaffMember.JobTitle)] = staff.JobTitle;
            record.Fields[nameof(StaffMember.EmploymentType)] = staff.EmploymentType.ToString();
            record.Fields[nameof(StaffMember.Status)] = staff.Status.ToString();
            record.Fields[nameof(StaffMember.HireDate)] = staff.HireDate?.ToString("O", CultureInfo.InvariantCulture);
            record.Fields[nameof(StaffMember.TerminationDate)] = staff.TerminationDate?.ToString("O", CultureInfo.InvariantCulture);
            record.Fields[nameof(StaffMember.DateOfBirth)] = staff.DateOfBirth?.ToString("O", CultureInfo.InvariantCulture);
            record.Fields[nameof(StaffMember.TimeZoneId)] = staff.TimeZoneId;
            record.Fields[nameof(StaffMember.WorkLocation)] = staff.WorkLocation;
            record.Fields[nameof(StaffMember.HomeBase)] = staff.HomeBase?.Address;
        }
    }

    public async Task AnonymizeAsync(PersonalDataAnonymizeContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var staff in await FindAsync(context.Subject, cancellationToken))
        {
            // A staff member keeps a name on schedules, timesheets and history, so it is replaced rather than cleared.
            // The store is written directly: a save through the manager would take the name back from the user.
            staff.DisplayName = context.ReplacementText ?? S["Former staff member"].Value;
            staff.Email = null;
            staff.Phone = null;
            staff.DateOfBirth = null;
            staff.HomeBase = null;

            await _store.UpdateAsync(staff, cancellationToken);
            context.Result.Count(Kind);
        }
    }

    private async Task<IReadOnlyList<StaffMember>> FindAsync(PersonalDataSubject subject, CancellationToken cancellationToken)
        => await _store.GetByUserIdsAsync(subject.UserIds, cancellationToken);
}

/// <summary>
/// Terminates staff members once their last working day has passed, so the modules that use staff clean up, and keeps
/// working staff with an account in the Staff role (staff saved before the role existed, or moved by a migration).
/// </summary>
[BackgroundTask(
    Title = "Staff upkeep",
    Schedule = "15 * * * *",
    Description = "Terminates staff members whose last working day has passed, and keeps working staff in the Staff role.")]
internal sealed class StaffTerminationBackgroundTask : IBackgroundTask
{
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var roles = serviceProvider.GetRequiredService<StaffRoles>();
        var manager = serviceProvider.GetRequiredService<IStaffMemberManager>();
        var resolver = serviceProvider.GetRequiredService<IStaffTimeZoneResolver>();
        var clock = serviceProvider.GetRequiredService<IClock>();
        var logger = serviceProvider.GetRequiredService<ILogger<StaffTerminationBackgroundTask>>();

        foreach (var status in new[] { StaffStatus.Active, StaffStatus.OnLeave, StaffStatus.Suspended })
        {
            var (_, people) = await manager.SearchAsync(new StaffQuery { Status = status }, 0, 5000, cancellationToken);

            foreach (var staff in people.Where(person => person.TerminationDate is not null))
            {
                var zone = Calendars.CalendarTimeZones.Find(await resolver.GetTimeZoneIdAsync(staff, cancellationToken));
                var today = NodaTime.Instant.FromDateTimeUtc(DateTime.SpecifyKind(clock.UtcNow, DateTimeKind.Utc)).InZone(zone).Date;

                if (staff.TerminationDate.Value >= DateOnly.FromDateTime(today.ToDateTimeUnspecified()))
                {
                    continue;
                }

                staff.Status = StaffStatus.Terminated;
                await manager.UpdateAsync(staff, cancellationToken: cancellationToken);

                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Staff member {StaffMemberId} was terminated after their last working day.", staff.ItemId);
                }
            }
        }

        if (roles.RoleName is null)
        {
            return;
        }

        for (var skip = 0; ; skip += 200)
        {
            var (count, page) = await manager.SearchAsync(new StaffQuery { HasUser = true }, skip, 200, cancellationToken);

            foreach (var staff in page)
            {
                await roles.SyncAsync(staff, cancellationToken);
            }

            if (skip + page.Count >= count || page.Count == 0)
            {
                break;
            }
        }
    }
}

/// <summary>
/// The geocoding target of a staff member's home base.
/// </summary>
public static class StaffGeocodeTargets
{
    /// <summary>
    /// A staff member's home base.
    /// </summary>
    public const string HomeBase = "StaffHomeBase";

    /// <summary>
    /// The name Workforce queued worker home bases under, before workers became staff members (same ids).
    /// </summary>
    public const string LegacyWorkerHomeBase = "WorkforceWorkerHomeBase";

    /// <summary>
    /// Gets whether a home base needs its coordinates found.
    /// </summary>
    /// <param name="location">The location.</param>
    /// <returns><see langword="true"/> when it has an address and no coordinates.</returns>
    public static bool NeedsGeocoding(StaffLocation location)
        => !string.IsNullOrWhiteSpace(location?.Address) && (location.Latitude is null || location.Longitude is null);
}

internal sealed class StaffHomeBaseGeocodeHandler : CatalogEntryHandlerBase<StaffMember>
{
    private readonly Lazy<IGeocodeQueue> _queue;

    public StaffHomeBaseGeocodeHandler(Lazy<IGeocodeQueue> queue)
    {
        _queue = queue;
    }

    public override Task CreatedAsync(CreatedContext<StaffMember> context, CancellationToken cancellationToken = default)
        => EnqueueAsync(context.Model, cancellationToken);

    public override Task UpdatedAsync(UpdatedContext<StaffMember> context, CancellationToken cancellationToken = default)
        => EnqueueAsync(context.Model, cancellationToken);

    private Task EnqueueAsync(StaffMember staff, CancellationToken cancellationToken)
        => StaffGeocodeTargets.NeedsGeocoding(staff.HomeBase)
            ? _queue.Value.EnqueueAsync(StaffGeocodeTargets.HomeBase, staff.ItemId, new GeocodeQuery { Text = staff.HomeBase.Address }, cancellationToken)
            : Task.CompletedTask;
}

internal class StaffHomeBaseGeocodeTarget : IGeocodeTarget
{
    private readonly IStaffMemberManager _manager;

    public StaffHomeBaseGeocodeTarget(IStaffMemberManager manager)
    {
        _manager = manager;
    }

    public virtual string TargetType => StaffGeocodeTargets.HomeBase;

    public async Task<bool> ApplyAsync(string targetId, GeocodeCandidate candidate, string source, CancellationToken cancellationToken = default)
    {
        var staff = await _manager.FindByIdAsync(targetId, cancellationToken);

        if (staff?.HomeBase is null || string.IsNullOrWhiteSpace(staff.HomeBase.Address))
        {
            return false;
        }

        staff.HomeBase.Latitude = candidate.Point.Latitude;
        staff.HomeBase.Longitude = candidate.Point.Longitude;
        await _manager.UpdateAsync(staff, null, cancellationToken);

        return true;
    }
}

internal sealed class LegacyWorkerHomeBaseGeocodeTarget : StaffHomeBaseGeocodeTarget
{
    public LegacyWorkerHomeBaseGeocodeTarget(IStaffMemberManager manager)
        : base(manager)
    {
    }

    public override string TargetType => StaffGeocodeTargets.LegacyWorkerHomeBase;
}
