using System.Security.Claims;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.PointOfSale.Models;
using Microsoft.AspNetCore.Authorization;
using OrchardCore;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.PointOfSale.Services;

/// <summary>
/// Opens, counts and closes register shifts, and says who may sell at a register.
/// </summary>
public sealed class PosShiftService
{
    // A shift's sales are read in pages this big when it's counted.
    private const int PageSize = 200;

    private readonly IRegisterStore _registers;
    private readonly IRegisterShiftStore _shifts;
    private readonly IOrderStore _orders;
    private readonly IAuthorizationService _authorizationService;
    private readonly IClock _clock;

    public PosShiftService(
        IRegisterStore registers,
        IRegisterShiftStore shifts,
        IOrderStore orders,
        IAuthorizationService authorizationService,
        IClock clock)
    {
        _registers = registers;
        _shifts = shifts;
        _orders = orders;
        _authorizationService = authorizationService;
        _clock = clock;
    }

    /// <summary>
    /// Returns whether a staff member may sell at a register: it's on, they may sell at registers, and they're in one
    /// of its roles when it names any.
    /// </summary>
    public async Task<bool> MaySellAtAsync(ClaimsPrincipal user, Register register)
        => register is { IsEnabled: true } &&
            await _authorizationService.AuthorizeAsync(user, PosPermissions.SellAtRegister) &&
            (register.Roles is not { Count: > 0 } roles || roles.Any(user.IsInRole) || await _authorizationService.AuthorizeAsync(user, PosPermissions.ManageRegisters));

    /// <summary>
    /// Returns the registers a staff member may sell at.
    /// </summary>
    public async Task<IReadOnlyList<Register>> ListForAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        var available = new List<Register>();

        foreach (var register in (await _registers.GetAllAsync(cancellationToken)).OrderBy(register => register.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (await MaySellAtAsync(user, register))
            {
                available.Add(register);
            }
        }

        return available;
    }

    /// <summary>
    /// Opens a shift at a register with the cash put in its drawer. Returns <see langword="null"/> when one is open.
    /// </summary>
    public async Task<RegisterShift> OpenAsync(Register register, decimal openingFloat, ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(register);

        if (await _shifts.FindOpenAsync(register.ItemId, cancellationToken) is not null)
        {
            return null;
        }

        var shift = new RegisterShift
        {
            ItemId = IdGenerator.GenerateId(),
            RegisterId = register.ItemId,
            RegisterName = register.Name,
            LocationId = register.LocationId,
            Currency = register.Currency,
            Status = RegisterShiftStatus.Open,
            OpenedByUserId = user.FindFirstValue(ClaimTypes.NameIdentifier),
            OpenedByName = GetName(user),
            OpenedUtc = _clock.UtcNow,
            OpeningFloat = Math.Max(0m, openingFloat),
        };

        await _shifts.CreateAsync(shift, cancellationToken);

        return shift;
    }

    /// <summary>
    /// Records cash put in the drawer (a positive amount) or taken out (a negative one).
    /// </summary>
    public async Task<bool> MoveCashAsync(RegisterShift shift, decimal amount, string reason, ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        if (shift is not { Status: RegisterShiftStatus.Open } || amount == 0m)
        {
            return false;
        }

        shift.CashMovements.Add(new RegisterCashMovement
        {
            Amount = amount,
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            ByUserId = user?.FindFirstValue(ClaimTypes.NameIdentifier),
            ByName = user is null ? null : GetName(user),
            Utc = _clock.UtcNow,
        });

        await _shifts.UpdateAsync(shift, cancellationToken);

        return true;
    }

    /// <summary>
    /// Returns what a shift has sold so far, and how it was paid, from its orders.
    /// </summary>
    public async Task<RegisterShiftSummary> SummarizeAsync(RegisterShift shift, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(shift);

        var summary = new RegisterShiftSummary();

        for (var skip = 0; ; skip += PageSize)
        {
            var (_, page) = await _orders.PageAsync(new OrderQuery
            {
                ShiftId = shift.ItemId,
                Statuses = [OrderStatus.Paid],
            }, skip, PageSize, cancellationToken);

            foreach (var order in page)
            {
                var total = order.Total ?? order.Subtotal;
                var cash = (order.Tenders ?? []).Where(tender => tender.ProviderKey == PosConstants.CashTenderKey).Sum(tender => tender.Amount);
                var other = (order.Tenders ?? []).Where(tender => tender.ProviderKey != PosConstants.CashTenderKey).Sum(tender => tender.Amount);

                summary.Sales++;
                summary.Total += total;
                summary.Cash += cash;
                summary.Other += other;
                summary.Card += Math.Max(0m, total - cash - other);
                summary.Refunded += order.RefundedAmount;
            }

            if (page.Count < PageSize)
            {
                break;
            }
        }

        return summary;
    }

    /// <summary>
    /// Closes a shift with the cash counted in its drawer, keeping what it sold and what the drawer should have held.
    /// </summary>
    public async Task<bool> CloseAsync(RegisterShift shift, decimal countedCash, string note, ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        if (shift is not { Status: RegisterShiftStatus.Open })
        {
            return false;
        }

        var summary = await SummarizeAsync(shift, cancellationToken);

        shift.Summary = summary;
        shift.ExpectedCash = shift.GetExpectedCash(summary.Cash);
        shift.CountedCash = countedCash;
        shift.CloseNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        shift.ClosedByUserId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        shift.ClosedByName = GetName(user);
        shift.ClosedUtc = _clock.UtcNow;
        shift.Status = RegisterShiftStatus.Closed;

        await _shifts.UpdateAsync(shift, cancellationToken);

        return true;
    }

    internal static string GetName(ClaimsPrincipal user)
        => user.Identity?.Name ?? user.FindFirstValue("name") ?? user.FindFirstValue("client_id") ?? "API";
}
