using System.Security.Claims;
using CrestApps.OrchardCore.PointOfSale.Models;
using CrestApps.OrchardCore.PointOfSale.Services;
using CrestApps.OrchardCore.Staff.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.PointOfSale.Staff;

/// <summary>
/// Makes the person who opens a register a staff member with the Cashier capability. It runs after the request, so
/// opening the register never waits for it, and a failure never stops a sale.
/// </summary>
internal sealed class CashierStaffHandler : IRegisterShiftHandler
{
    public const string CashierCapability = "Cashier";

    public Task OpenedAsync(RegisterShift shift, ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        var userId = shift.OpenedByUserId;

        if (string.IsNullOrEmpty(userId))
        {
            return Task.CompletedTask;
        }

        var name = shift.OpenedByName;

        ShellScope.AddDeferredTask(async scope =>
        {
            try
            {
                await scope.ServiceProvider.GetRequiredService<IStaffAccountService>().EnsureAsync(userId, name ?? userId, [CashierCapability], staff => staff.JobTitle ??= "Cashier");
            }
            catch (Exception ex)
            {
                scope.ServiceProvider.GetRequiredService<ILogger<CashierStaffHandler>>().LogError(ex, "The cashier {UserId} could not be made a staff member.", userId);
            }
        });

        return Task.CompletedTask;
    }
}
