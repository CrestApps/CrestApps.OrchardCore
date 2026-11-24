using CrestApps.Core;
using CrestApps.OrchardCore.Dispatch.Core.Indexes;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Workforce.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.Dispatch.Migrations;

internal sealed class DispatchMigrations : DataMigration
{
    private const int PageSize = 200;

    public async Task<int> CreateAsync()
    {
        await DispatchIndexSchema.CreateAsync(SchemaBuilder);

        return 2;
    }

    /// <summary>
    /// Dispatch now sends only staff members with the Technician capability. Before, it sent every Workforce worker, and
    /// workers became staff members carrying their Workforce settings, so those people are given the capability; an
    /// admin can untick it for anyone who isn't a technician.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public static int UpdateFrom1()
    {
        // After every migration of this start, so the workers have already moved to Staff.
        ShellScope.AddDeferredTask(async scope =>
        {
            var manager = scope.ServiceProvider.GetRequiredService<IStaffMemberManager>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<DispatchMigrations>>();
            var given = 0;

            try
            {
                for (var skip = 0; ; skip += PageSize)
                {
                    var (_, people) = await manager.SearchAsync(new StaffQuery(), skip, PageSize);

                    foreach (var staff in people)
                    {
                        if (staff.HasCapability(DispatchConstants.TechnicianCapability) || !staff.TryGet<WorkforceStaffSettings>(out _))
                        {
                            continue;
                        }

                        staff.Capabilities.Add(DispatchConstants.TechnicianCapability);
                        await manager.UpdateAsync(staff);
                        given++;
                    }

                    if (people.Count < PageSize)
                    {
                        break;
                    }
                }

                if (given > 0 && logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("{Count} former Workforce workers were given the Technician capability.", given);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "The former Workforce workers could not be given the Technician capability.");
            }
        });

        return 2;
    }
}
