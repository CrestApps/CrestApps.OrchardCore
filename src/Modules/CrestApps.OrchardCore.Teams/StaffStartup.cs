using CrestApps.OrchardCore.Staff;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Teams.Staff;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Teams;

/// <summary>
/// Puts staff in teams: turns team memberships on for Staff and adds the Teams card to the staff editor.
/// </summary>
[RequireFeatures(StaffConstants.Features.Staff)]
public sealed class StaffStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<StaffOptions>(options => options.UsesTeams = true);
        services.AddDisplayDriver<StaffMember, StaffTeamsDisplayDriver>();
    }
}
