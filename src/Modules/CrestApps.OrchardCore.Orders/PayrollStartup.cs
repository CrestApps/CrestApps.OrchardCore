using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Orders.Payroll;
using CrestApps.OrchardCore.Payroll;
using CrestApps.OrchardCore.Staff;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Orders;

/// <summary>
/// With Pay Setup and Staff on: the commission staff earn on the orders they make becomes pay inputs.
/// </summary>
[RequireFeatures(PayrollConstants.Features.PaySetup, StaffConstants.Features.Staff)]
public sealed class PayrollStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddScoped<OrderReportReader>();
        services.AddPayInputSource<OrderCommissionPayInputSource>();
    }
}
