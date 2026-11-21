using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.WorkOrders;

/// <summary>
/// Picks the technicians a job should or must not go to from staff.
/// </summary>
[RequireFeatures(CrestApps.OrchardCore.Staff.StaffConstants.Features.Staff)]
public sealed class StaffTechniciansStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
    }
}
