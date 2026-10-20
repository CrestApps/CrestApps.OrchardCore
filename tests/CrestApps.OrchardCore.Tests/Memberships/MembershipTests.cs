using System.Security.Claims;
using CrestApps.OrchardCore.Carts.Core.Services;
using CrestApps.OrchardCore.Memberships.Handlers;
using CrestApps.OrchardCore.Memberships.Models;
using CrestApps.OrchardCore.Memberships.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.Http;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Memberships;

/// <summary>
/// A membership lasts its days for each unit bought, buying again while a member adds them, and it's bought signed in.
/// </summary>
public sealed class MembershipTests
{
    private static readonly DateTime _now = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void AMembership_LastsItsDaysForEachUnit_AndBuyingAgainAddsThemToItsEnd()
    {
        Assert.Equal(_now.AddDays(365), MembershipPeriods.Extend(_now, null, 365, 1));
        Assert.Equal(_now.AddDays(60), MembershipPeriods.Extend(_now, null, 30, 2));

        // Still a member for 100 days: a year more runs from then, not from today.
        Assert.Equal(_now.AddDays(465), MembershipPeriods.Extend(_now, _now.AddDays(100), 365, 1));

        // One that ran out starts again from today.
        Assert.Equal(_now.AddDays(30), MembershipPeriods.Extend(_now, _now.AddDays(-5), 30, 1));

        // No days is for good.
        Assert.Null(MembershipPeriods.Extend(_now, null, 0, 1));
    }

    [Fact]
    public async Task AGuest_CantPutAMembershipInTheirCart_AndASignedInBuyerCan()
    {
        var item = new ContentItem { ContentItemId = "plan", DisplayText = "Annual Membership" };

        item.Weld(new MembershipPart { RoleNames = ["Member"], DurationDays = 365 });

        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        var handler = new MembershipCartLineHandler(accessor, new PassThroughStringLocalizer<MembershipCartLineHandler>());
        var guest = new CartLinePrepareContext { ContentItem = item, Title = item.DisplayText };

        await handler.PrepareAsync(guest, TestContext.Current.CancellationToken);

        Assert.NotNull(guest.Problem);

        accessor.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "u1")], "Test"));

        var member = new CartLinePrepareContext { ContentItem = item, Title = item.DisplayText };

        await handler.PrepareAsync(member, TestContext.Current.CancellationToken);

        Assert.Null(member.Problem);
    }
}
