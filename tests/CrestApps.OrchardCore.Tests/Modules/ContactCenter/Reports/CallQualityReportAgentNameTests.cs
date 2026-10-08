using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Reports.Providers;
using CrestApps.OrchardCore.ContactCenter.Reports.Services;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.Reports;

/// <summary>
/// The call quality report named an agent "(Unknown agent)" whenever their profile carried no user name, which every
/// profile made on first sign-in did. The report reads the name from the user account instead.
/// </summary>
public sealed class CallQualityReportAgentNameTests
{
    private static readonly DateTime _observed = new(2026, 9, 24, 14, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task RunAsync_AgentProfileWithoutAUserName_IsNamedFromTheUserAccount()
    {
        // Arrange
        var records = new Mock<ICallQualityRecordStore>();
        records
            .Setup(store => store.GetObservedBetweenAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new CallQualityRecord
                {
                    ItemId = "r1",
                    AgentId = "a1",
                    Source = CallQualitySource.Browser,
                    LegRole = CallPartyRole.Agent,
                    Rating = CallQualityRating.Good,
                    ProviderCallControlId = "leg-1",
                    Mos = 4.3,
                    ObservedUtc = _observed,
                },
            ]);

        var profiles = new Mock<IAgentProfileStore>();
        profiles
            .Setup(store => store.GetAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new AgentProfile { ItemId = "a1", UserId = "u1" }]);

        var user = new Mock<IUser>();
        user.SetupGet(value => value.UserName).Returns("mike");
        var userManager = new Mock<UserManager<IUser>>(new Mock<IUserStore<IUser>>().Object, null, null, null, null, null, null, null, null);
        userManager.Setup(manager => manager.FindByIdAsync("u1")).ReturnsAsync(user.Object);

        var guard = new Mock<IContactCenterReportCapabilityGuard>();
        guard
            .Setup(value => value.GetMissingFeaturesAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var localizer = new Mock<IStringLocalizer<CallQualityReportProvider>>();
        localizer.Setup(value => value[It.IsAny<string>()]).Returns<string>(name => new LocalizedString(name, name));
        localizer.Setup(value => value[It.IsAny<string>(), It.IsAny<object[]>()]).Returns<string, object[]>((name, _) => new LocalizedString(name, name));

        var report = new CallQualityReportProvider(
            new Mock<IContactCenterReportingService>().Object,
            guard.Object,
            records.Object,
            Mock.Of<IInteractionStore>(),
            Mock.Of<IInteractionEventStore>(),
            profiles.Object,
            userManager.Object,
            localizer.Object);

        var filter = new ReportFilter();
        filter.SetDateRange(new ReportDateRange { FromUtc = _observed.AddHours(-1), ToUtc = _observed.AddHours(1) });

        // Act
        var document = await report.RunAsync(new ReportContext(filter), TestContext.Current.CancellationToken);

        // Assert
        var agentCells = document.Sections
            .SelectMany(section => section.Rows)
            .SelectMany(row => row.Cells)
            .ToArray();

        Assert.Contains(ReportValue.UserDisplayName("mike", "(Unknown agent)"), agentCells);
        Assert.DoesNotContain("(Unknown agent)", agentCells);
    }
}
