using System.Security.Claims;
using CrestApps.OrchardCore.PointOfSale.Models;

namespace CrestApps.OrchardCore.PointOfSale.Services;

/// <summary>
/// Hears about register shifts, so other modules can follow who works the registers, such as Staff marking the person
/// who opens a register as a cashier.
/// </summary>
public interface IRegisterShiftHandler
{
    /// <summary>
    /// Called after a shift is opened and saved.
    /// </summary>
    /// <param name="shift">The shift.</param>
    /// <param name="user">The person who opened it.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task OpenedAsync(RegisterShift shift, ClaimsPrincipal user, CancellationToken cancellationToken = default);
}
