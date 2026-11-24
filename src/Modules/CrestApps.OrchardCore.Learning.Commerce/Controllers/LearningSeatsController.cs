using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Accounts.Services;
using CrestApps.OrchardCore.Learning.Commerce.Models;
using CrestApps.OrchardCore.Learning.Commerce.Services;
using CrestApps.OrchardCore.Learning.Commerce.ViewModels;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.DisplayManagement.Notify;

namespace CrestApps.OrchardCore.Learning.Commerce.Controllers;

/// <summary>
/// The seats an account bought: an admin of the account gives them to members, invites people to them, takes them back
/// and sees the progress of the people holding them, and nobody else's.
/// </summary>
[Authorize]
public sealed class LearningSeatsController : Controller
{
    private readonly ILicensePoolService _pools;
    private readonly IAccountMemberService _members;
    private readonly IEnrollmentStore _enrollments;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    public LearningSeatsController(ILicensePoolService pools, IAccountMemberService members, IEnrollmentStore enrollments, INotifier notifier, IHtmlLocalizer<LearningSeatsController> htmlLocalizer)
    {
        _pools = pools;
        _members = members;
        _enrollments = enrollments;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    [HttpGet("account/learning-seats", Name = "LearningSeats")]
    public async Task<IActionResult> Index()
    {
        var (account, isAdmin) = await FindAccountAsync();

        if (account is null || !isAdmin)
        {
            return Forbid();
        }

        var members = account.TryGet<AccountMembersPart>(out var part) ? part.Members ?? [] : [];
        var pools = new List<LearningSeatsPool>();

        foreach (var pool in await _pools.ListForAccountAsync(account.ContentItemId, HttpContext.RequestAborted))
        {
            var seats = (await _pools.ListSeatsAsync(pool.ItemId, HttpContext.RequestAborted)).Where(seat => seat.State != LicenseSeatState.Removed).ToArray();
            var rows = new List<LearningSeatsRow>();

            foreach (var seat in seats)
            {
                var held = seat.State == LicenseSeatState.Assigned
                    ? await _enrollments.ListByGrantAsync(LearningCommerceConstants.SeatSource, seat.ItemId, HttpContext.RequestAborted)
                    : [];
                var enrollment = held.Count > 0 ? held[0] : null;

                rows.Add(new LearningSeatsRow
                {
                    Seat = seat,
                    Name = members.FirstOrDefault(member => member.UserId == seat.UserId)?.UserName ?? seat.Email,
                    Enrollment = enrollment,
                });
            }

            pools.Add(new LearningSeatsPool
            {
                Pool = pool,
                Remaining = await _pools.GetRemainingAsync(pool, HttpContext.RequestAborted),
                Seats = rows,
                MembersWithoutSeat = members.Where(member => seats.All(seat => seat.UserId != member.UserId)).ToArray(),
            });
        }

        return View(new LearningSeatsViewModel { AccountName = account.DisplayText, Pools = pools });
    }

    [HttpPost("account/learning-seats/{poolId}/give")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Give(string poolId, string userId)
    {
        if (!await OwnsPoolAsync(poolId))
        {
            return Forbid();
        }

        var result = await _pools.GiveAsync(poolId, userId, User.Identity?.Name, HttpContext.RequestAborted);

        await NotifyAsync(result, H["The seat has been given."]);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("account/learning-seats/{poolId}/invite")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Invite(string poolId, string email)
    {
        if (!await OwnsPoolAsync(poolId))
        {
            return Forbid();
        }

        var result = await _pools.InviteAsync(poolId, email, User.Identity?.Name, HttpContext.RequestAborted);

        await NotifyAsync(result, result.Seat?.State == LicenseSeatState.Assigned ? H["{0} is already a member and has the seat.", email ?? string.Empty] : H["{0} is invited. The seat is held until they accept.", email ?? string.Empty]);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("account/learning-seats/seats/{seatId}/remove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(string seatId, string poolId)
    {
        if (!await OwnsPoolAsync(poolId) || !(await _pools.ListSeatsAsync(poolId, HttpContext.RequestAborted)).Any(seat => seat.ItemId == seatId))
        {
            return Forbid();
        }

        var result = await _pools.RemoveAsync(seatId, User.Identity?.Name, HttpContext.RequestAborted);

        await NotifyAsync(result, H["The seat has been taken back and is free again."]);

        return RedirectToAction(nameof(Index));
    }

    private async Task NotifyAsync(LicenseSeatResult result, LocalizedHtmlString success)
    {
        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(success);
        }
        else
        {
            await _notifier.ErrorAsync(H["{0}", result.Error ?? string.Empty]);
        }
    }

    private async Task<bool> OwnsPoolAsync(string poolId)
    {
        var (account, isAdmin) = await FindAccountAsync();
        var pool = await _pools.FindAsync(poolId, HttpContext.RequestAborted);

        return account is not null && isAdmin && pool?.AccountId == account.ContentItemId;
    }

    private async Task<(ContentItem Account, bool IsAdmin)> FindAccountAsync()
    {
        var membership = await _members.FindMembershipAsync(User, HttpContext.RequestAborted);

        return (membership?.Account, membership?.IsAdmin == true);
    }
}
