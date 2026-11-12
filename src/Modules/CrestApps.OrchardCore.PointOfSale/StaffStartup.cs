using CrestApps.OrchardCore.PointOfSale.Services;
using CrestApps.OrchardCore.PointOfSale.Staff;
using CrestApps.OrchardCore.Staff;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.PointOfSale;

/// <summary>
/// Makes cashiers staff members: the Cashier capability, given to whoever opens a register.
/// </summary>
[RequireFeatures(StaffConstants.Features.Staff)]
public sealed class StaffStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddStaffCapability(CashierStaffHandler.CashierCapability, LocalizationSource.Create("Cashier", typeof(StaffStartup)), LocalizationSource.Create("Runs a register. Opening a register makes a person a cashier.", typeof(StaffStartup)), 40);
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IRegisterShiftHandler, CashierStaffHandler>());
    }
}
