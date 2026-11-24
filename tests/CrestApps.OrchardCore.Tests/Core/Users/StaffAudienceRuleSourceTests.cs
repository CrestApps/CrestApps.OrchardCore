using CrestApps.OrchardCore.Staff.Core.Services;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Users;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Core.Users;

/// <summary>
/// The Staff audience rules count days in the site's time zone and keep only active staff linked to a user.
/// </summary>
public sealed class StaffAudienceRuleSourceTests
{
    [Fact]
    public async Task HireDateRule_MatchesActiveLinkedStaffHiredAtLeastTheDaysAgo_OnTheSiteDay()
    {
        // Arrange: it is still the 31st on the site while UTC is already on the 1st.
        var members = new List<StaffMember>
        {
            new() { ItemId = "s1", UserId = "ana", HireDate = new DateOnly(2026, 10, 1), Status = StaffStatus.Active },
            new() { ItemId = "s2", UserId = "ben", HireDate = new DateOnly(2026, 10, 2), Status = StaffStatus.Active },
            new() { ItemId = "s3", UserId = "cid", Status = StaffStatus.Active },
        };
        var staff = new Mock<IStaffMemberManager>();

        staff.Setup(manager => manager.SearchAsync(It.Is<StaffQuery>(query => query.Status == StaffStatus.Active && query.HasUser == true), 0, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((members.Count, members));
        staff.Setup(manager => manager.FindByUserIdAsync("ana", It.IsAny<CancellationToken>())).ReturnsAsync(members[0]);
        staff.Setup(manager => manager.FindByUserIdAsync("ben", It.IsAny<CancellationToken>())).ReturnsAsync(members[1]);

        var clock = new Mock<ILocalClock>();

        clock.Setup(value => value.GetLocalNowAsync()).ReturnsAsync(new DateTimeOffset(2026, 10, 31, 20, 0, 0, TimeSpan.FromHours(-7)));

        var source = new StaffHireDateAudienceRuleSource(staff.Object, clock.Object, new Mock<IStringLocalizer<StaffHireDateAudienceRuleSource>>().Object);
        var rule = new UserAudienceRule { Source = source.Name, Number = 30 };

        // Act
        var members30 = await source.ResolveUserIdsAsync(rule, TestContext.Current.CancellationToken);
        var ana = await source.IsMatchAsync(rule, "ana", TestContext.Current.CancellationToken);
        var ben = await source.IsMatchAsync(rule, "ben", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["ana"], members30);
        Assert.True(ana);
        Assert.False(ben);
    }
}
