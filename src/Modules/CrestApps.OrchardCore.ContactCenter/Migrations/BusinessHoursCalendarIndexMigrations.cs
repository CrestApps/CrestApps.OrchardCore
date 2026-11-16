using System.Diagnostics.CodeAnalysis;
using CrestApps.OrchardCore.ContactCenter.Legacy;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// The Contact Center Business Hours feature's migrations. The feature kept its own business-hours calendars, with an
/// index table, until its hours moved to the Business Hours module; a site that kept calendars gets each one copied to a
/// Business Hours schedule with the same id.
/// </summary>
/// <remarks>
/// The class keeps its name, which Orchard Core records the migration's version under. The old calendar documents and
/// their index table stay, unread, so a node still running the previous version keeps working during a rolling upgrade.
/// </remarks>
internal sealed class BusinessHoursCalendarIndexMigrations : DataMigration
{
    private readonly IBusinessHoursCalendarUpgrade _upgrade;

    public BusinessHoursCalendarIndexMigrations(IBusinessHoursCalendarUpgrade upgrade)
    {
        _upgrade = upgrade;
    }

    /// <summary>
    /// A new site keeps its hours in the Business Hours module from the start, so there is nothing to create.
    /// </summary>
    /// <returns>The migration version number.</returns>
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Orchard Core looks the create step up as a public instance method.")]
    public Task<int> CreateAsync()
        => Task.FromResult(3);

    /// <summary>
    /// Used to add an index to the calendars' table for reading a calendar by its identifier. Nothing reads that table
    /// any more, so the step only moves the version on.
    /// </summary>
    /// <returns>The migration version number.</returns>
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Orchard Core looks update steps up as public instance methods.")]
    public Task<int> UpdateFrom1Async()
        => Task.FromResult(2);

    /// <summary>
    /// Copies every calendar to a Business Hours schedule with the same id, so the queues, entry points, dialing
    /// profiles and activity batches that name a calendar keep their hours, and lets the roles that edited calendars
    /// edit the schedules. The Business Hours feature is a dependency of this one, so its store is there.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom2Async()
    {
        await _upgrade.MoveCalendarsAsync();
        await _upgrade.GrantSchedulePermissionsAfterCommitAsync();

        return 3;
    }
}
