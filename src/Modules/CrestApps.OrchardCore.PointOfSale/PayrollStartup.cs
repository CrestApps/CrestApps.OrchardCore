using CrestApps.OrchardCore.Payroll;
using CrestApps.OrchardCore.PointOfSale.Payroll;
using CrestApps.OrchardCore.Staff;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.PointOfSale;

/// <summary>
/// With Pay Setup and Staff on: the tips cashiers declare at the close of a shift become pay inputs.
/// </summary>
[RequireFeatures(PayrollConstants.Features.PaySetup, StaffConstants.Features.Staff)]
public sealed class PayrollStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddPayInputSource<PosTipsPayInputSource>();
    }
}
