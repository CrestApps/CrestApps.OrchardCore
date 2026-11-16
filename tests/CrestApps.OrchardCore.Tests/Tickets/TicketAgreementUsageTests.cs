using CrestApps.Core;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Agreements.Models;
using CrestApps.OrchardCore.Agreements.Services;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Tickets;
using CrestApps.OrchardCore.Tickets.Agreements;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.TimeTracker;
using CrestApps.OrchardCore.TimeTracker;
using CrestApps.OrchardCore.TimeTracker.Core.Indexes;
using CrestApps.OrchardCore.TimeTracker.Core.Services;
using CrestApps.OrchardCore.TimeTracker.Models;
using CrestApps.OrchardCore.TimeTracker.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Notifications;
using OrchardCore.Users;
using OrchardCore.Users.Services;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Tickets;

/// <summary>
/// Tickets on agreements: the billable time logged on an agreement's tickets counts against its hours (not the time in a
/// desk's stage for time not billed, nor a ticket marked no charge); a new ticket takes the earliest agreement in force
/// whose ticket terms cover its desk and type; and the account's manager is told when the hours pass a share.
/// </summary>
public sealed class TicketAgreementUsageTests
{
    private static readonly DateTime _day = CalendarTestHarness.Now.Date;

    [Fact]
    public async Task TheBillableTimeOnAnAgreementsTickets_CountsAgainstIt()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var (billed, notBilled) = await harness.RunAsync<(string, string)>(async services =>
        {
            var stages = services.GetRequiredService<TimeStageManager>();
            var first = await stages.NewAsync();
            first.Name = "Tickets";
            await stages.CreateAsync(first);
            var second = await stages.NewAsync();
            second.Name = "Tickets, not billed";
            await stages.CreateAsync(second);

            var support = (await services.GetRequiredService<ICatalog<TicketDesk>>().GetAllAsync()).Single(desk => desk.ItemId == "support");
            support.Put(new TicketDeskTimeSettings { StageId = first.ItemId, NotBilledStageId = second.ItemId });

            var session = services.GetRequiredService<ISession>();
            await session.SaveAsync(Ticket("a1", "gold", null));
            await session.SaveAsync(Ticket("a2", "gold", TicketBillingType.NoCharge));
            await session.SaveAsync(Ticket("a3", null, null));

            return (first.ItemId, second.ItemId);
        });
        await harness.RunAsync(async services =>
        {
            var tracker = services.GetRequiredService<ITimeTrackerService>();

            foreach (var (ticket, stage, from, to) in new[] { ("a1", billed, 1.0, 2.0), ("a1", notBilled, 2.0, 2.5), ("a2", billed, 3.0, 3.75), ("a3", billed, 4.0, 5.0) })
            {
                var result = await tracker.AddAsync(new TimeEntry { UserId = "ana", UserName = "Ana", StageId = stage, StartUtc = _day.AddHours(from), EndUtc = _day.AddHours(to), Source = TimeTrackerConstants.Sources.Ticket, SourceRef = TicketTime.SourceRefOf(ticket) }, "ana", null);
                Assert.True(result.Succeeded, string.Join(" ", result.Errors));
            }

            return true;
        });

        // Act
        var usage = await harness.RunAsync(services => ActivatorUtilities.CreateInstance<TicketAgreementUsageProvider>(services)
            .ListAsync(new Agreement { ItemId = "gold" }, _day, _day.AddDays(1)), commit: false);

        // Assert
        var entry = Assert.Single(usage);
        Assert.Equal(60, entry.Minutes);
        Assert.Equal("a1 a1", entry.Description);
    }

    [Fact]
    public async Task ANewTicket_TakesTheEarliestAgreementWhoseTermsCoverIt()
    {
        // Arrange (a field-service agreement without ticket terms; one for the IT desk only; one for every desk)
        var withoutTerms = new Agreement { ItemId = "visits", Name = "Visits", AccountId = "contoso", StartDate = new DateOnly(2026, 1, 1) };
        var itOnly = new Agreement { ItemId = "it", Name = "IT", AccountId = "contoso", StartDate = new DateOnly(2026, 2, 1) };
        itOnly.Put(new TicketAgreementTerms { DeskIds = ["it"] });
        var every = new Agreement { ItemId = "every", Name = "Support", AccountId = "contoso", StartDate = new DateOnly(2026, 3, 1) };
        every.Put(new TicketAgreementTerms { ServiceLevelPolicyId = "gold-policy" });
        var balances = new Mock<IAgreementBalanceService>();
        balances.Setup(service => service.ListInForceAsync("contoso", It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>())).ReturnsAsync([withoutTerms, itOnly, every]);
        var resolver = new TicketAgreementResolver(balances.Object, Mock.Of<IAgreementManager>());

        // Act
        var onSupport = await resolver.FindForNewTicketAsync("contoso", "support", "Ticket", TestContext.Current.CancellationToken);
        var onIt = await resolver.FindForNewTicketAsync("contoso", "it", "Ticket", TestContext.Current.CancellationToken);
        var listed = await resolver.ListForAccountAsync("contoso", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(("every", "gold-policy"), (onSupport.AgreementId, onSupport.ServiceLevelPolicyId));
        Assert.Equal("it", onIt.AgreementId);
        Assert.Equal(["it", "every"], listed.Select(agreement => agreement.AgreementId));
    }

    [Fact]
    public async Task TheAccountsManager_IsToldWhenTheHoursPassAShare()
    {
        // Arrange (Contoso's account team names staff member s1, who signs in as user u1)
        var account = new ContentItem { ContentItemId = "contoso", ContentType = "Account", DisplayText = "Contoso" };
        account.Apply(new AccountTeamPart { OwnerStaffId = "s1" });
        var contentManager = new Mock<IContentManager>();
        contentManager.Setup(manager => manager.GetAsync("contoso", It.IsAny<VersionOptions>())).ReturnsAsync(account);
        var staff = new Mock<IStaffDirectory>();
        staff.Setup(directory => directory.GetAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, StaffMember> { ["s1"] = new StaffMember { ItemId = "s1", UserId = "u1" } });
        var user = Mock.Of<IUser>();
        var users = new Mock<IUserService>();
        users.Setup(service => service.GetUserByUniqueIdAsync("u1")).ReturnsAsync(user);
        var sent = new List<(IUser User, INotificationMessage Message)>();
        var notifications = new Mock<INotificationService>();
        notifications.Setup(service => service.SendAsync(It.IsAny<object>(), It.IsAny<INotificationMessage>(), It.IsAny<CancellationToken>()))
            .Callback((object to, INotificationMessage message, CancellationToken _) => sent.Add(((IUser)to, message)))
            .Returns(Task.FromResult(default(NotificationSendResult)));
        var services = new ServiceCollection().AddSingleton(staff.Object).BuildServiceProvider();
        var notifier = new CrestApps.OrchardCore.Agreements.Services.AgreementAccountManagerNotifier(
            contentManager.Object,
            notifications.Object,
            users.Object,
            services,
            new PassThroughStringLocalizer<CrestApps.OrchardCore.Agreements.Services.AgreementAccountManagerNotifier>());
        var agreement = new Agreement { ItemId = "gold", Number = "SA-00001", Name = "Gold support", AccountId = "contoso" };

        // Act
        await notifier.OnHoursUsedAsync(agreement, 80, new AgreementBalance { CoveredMinutes = 600, UsedMinutes = 510 }, TestContext.Current.CancellationToken);

        // Assert
        var (to, message) = Assert.Single(sent);
        Assert.Same(user, to);
        Assert.Equal("Contoso has used 80% of the hours of agreement SA-00001", message.Subject);
    }

    private static Task<CalendarTestHarness> CreateAsync()
        => TicketReportDataSourceTests.CreateAsync(
            services =>
            {
                services.AddTimeTrackerCore();
                services.AddSingleton(new Mock<IExternalTimeClock>().Object);
            },
            TimeTrackerIndexSchema.CreateAsync,
            [new TimeTrackerIndexProvider(), new TimeCategoryIndexProvider(), new TimeStageIndexProvider()],
            [TimeTrackerConstants.CollectionName]);

    private static ContentItem Ticket(string id, string agreementId, TicketBillingType? billingType)
    {
        var item = new ContentItem
        {
            ContentItemId = id,
            ContentType = TicketsConstants.DefaultContentType,
            Latest = true,
            Published = true,
            DisplayText = id,
            CreatedUtc = _day,
            ModifiedUtc = _day,
        };
        item.Apply(new TicketPart
        {
            Number = id,
            DeskId = "support",
            StatusId = "open",
            StatusCategory = TicketStatusCategory.Open,
            PriorityId = "normal",
            AccountId = "contoso",
            AgreementId = agreementId,
            BillingType = billingType,
        });

        return item;
    }
}
