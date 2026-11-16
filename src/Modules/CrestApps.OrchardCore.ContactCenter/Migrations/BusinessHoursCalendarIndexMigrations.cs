using CrestApps.OrchardCore.BusinessHours;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Legacy;
using CrestApps.OrchardCore.Core.Security;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundJobs;
using OrchardCore.Data.Migration;
using YesSql.Indexes;
using YesSql.Sql;

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
    /// <summary>
    /// The permission that let a role edit the calendars, which no longer exists.
    /// </summary>
    internal const string LegacyManagePermissionName = "ManageContactCenterBusinessHours";

    private readonly BusinessHoursCalendarMove _move;

    public BusinessHoursCalendarIndexMigrations(BusinessHoursCalendarMove move)
    {
        _move = move;
    }

    /// <summary>
    /// A new site keeps its hours in the Business Hours module from the start, so there is nothing to create.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public static int Create()
        => 3;

    /// <summary>
    /// Adds the index a calendar was found by when it was read by its own identifier.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        await IndexTableIndexCreation.CreateIfMissingAsync<BusinessHoursCalendarIndex>(
            SchemaBuilder,
            "IDX_BusinessHoursCalendarIndex_ItemId",
            ContactCenterStorage.CollectionName,
            "ItemId", "DocumentId");

        return 2;
    }

    /// <summary>
    /// Copies every calendar to a Business Hours schedule with the same id, so the queues, entry points, dialing
    /// profiles and activity batches that name a calendar keep their hours, and lets the roles that edited calendars
    /// edit the schedules. The Business Hours feature is a dependency of this one, so its store is there.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom2Async()
    {
        await _move.MoveAsync();

        // The roles are documents; they are written after the request that runs this step, once its transaction has
        // committed. Granting is idempotent, so a later run changes nothing more.
        await HttpBackgroundJob.ExecuteAfterEndOfRequestAsync("grant-business-hours-schedules", static scope => RolePermissionMigrations.GrantAsync(
            scope.ServiceProvider,
            new Dictionary<string, string[]>
            {
                [LegacyManagePermissionName] = [BusinessHoursPermissions.ManageBusinessHoursSchedules.Name],
                [ContactCenterPermissions.ManageContactCenter.Name] = [BusinessHoursPermissions.ManageBusinessHoursSchedules.Name],
            },
            scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger<BusinessHoursCalendarIndexMigrations>()));

        return 3;
    }

    // The index table the calendars had, named after this type. Nothing writes through it.
    private sealed class BusinessHoursCalendarIndex : MapIndex
    {
    }
}
