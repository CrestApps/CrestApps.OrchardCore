using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.PhoneNumbers;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.WorkOrders;
using CrestApps.OrchardCore.Tickets;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using CrestApps.OrchardCore.Tickets.WorkOrders;
using CrestApps.OrchardCore.Timeline.Models;
using CrestApps.OrchardCore.Timeline.Services;
using CrestApps.OrchardCore.WorkOrders;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Tickets;

/// <summary>
/// A ticket that needs someone on site becomes a work order with the ticket's subject, first message, account and
/// requester; the ticket links to it, and hears on its timeline when the work order is completed or canceled.
/// </summary>
public sealed class TicketWorkOrderTests
{
    [Fact]
    public void AWorkOrder_TakesTheTicketsSubjectAccountAndRequester()
    {
        // Arrange
        var ticket = Ticket("t1", new string('x', 130));
        var workOrder = new WorkOrder();

        // Act
        TicketWorkOrders.Fill(workOrder, ticket, "  The heater is cold.  ");

        // Assert
        Assert.Equal(new string('x', TicketWorkOrders.MaxSummary) + "…", workOrder.Summary);
        Assert.Equal("The heater is cold.", workOrder.Description);
        Assert.Equal("acme", workOrder.AccountId);
        Assert.Equal("contact-1", workOrder.ContactId);
        Assert.Equal("Dana", workOrder.Contact.Name);
        Assert.Equal("dana@contoso.com", workOrder.Contact.Email);
        Assert.Equal("+17025550100", workOrder.Contact.Phone);
        Assert.Equal("Las Vegas", workOrder.ServiceAddress.City);
        Assert.Equal(WorkOrderSource.Ticket, workOrder.Source);
        Assert.Equal("t1", workOrder.SourceId);
    }

    [Fact]
    public async Task ATicket_BecomesAWorkOrder_WithItsFirstMessage_AndLinksToIt()
    {
        // Arrange
        var now = new DateTime(2026, 11, 14, 9, 0, 0, DateTimeKind.Utc);
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(() => now);
        var tickets = new Mock<ITicketService>();
        tickets.Setup(service => service.GetAsync("t1")).ReturnsAsync(Ticket("t1", "No heat in the office"));
        tickets.Setup(service => service.LinkAsync("t1", It.IsAny<RecordRef>(), TicketLinkKinds.Related, It.IsAny<TicketActor>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketResult());
        await using var harness = await CalendarTestHarness.CreateAsync(
            services =>
            {
                services.AddSingleton(clock.Object);
                services.AddWorkOrdersWithServiceLevels();
                services.AddSingleton(tickets.Object);
                services.AddScoped<TicketWorkOrders>();
            },
            WorkOrderTestStores.CreateSchemaAsync,
            [.. WorkOrderTestStores.IndexProviders],
            WorkOrderTestStores.Collections);
        var typeId = await harness.RunAsync(async services =>
        {
            var timeline = services.GetRequiredService<ITimelineService>();
            await timeline.AddAsync(Reply("The heater is cold.", now.AddHours(-2)), TestContext.Current.CancellationToken);
            await timeline.AddAsync(Reply("Still cold this morning.", now.AddHours(-1)), TestContext.Current.CancellationToken);

            var types = services.GetRequiredService<WorkOrderSettingManager<WorkOrderType>>();
            var type = await types.NewAsync();
            type.Name = "Heating repair";
            await types.CreateAsync(type);

            return type.ItemId;
        });

        // Act
        var result = await harness.RunAsync(services => services.GetRequiredService<TicketWorkOrders>()
            .CreateAsync("t1", typeId, WorkOrderPriority.High, TicketActor.User("u1", "ana"), TestContext.Current.CancellationToken));
        var refused = await harness.RunAsync(services => services.GetRequiredService<TicketWorkOrders>()
            .CreateAsync("t1", "no-such-type", null, TicketActor.User("u1", "ana"), TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.Succeeded);
        var stored = await harness.RunAsync(async services => await services.GetRequiredService<WorkOrderService>().FindAsync(result.WorkOrder.ItemId), commit: false);
        Assert.Equal("No heat in the office", stored.Summary);
        Assert.Equal("The heater is cold.", stored.Description);
        Assert.Equal(WorkOrderPriority.High, stored.Priority);
        Assert.Equal(WorkOrderSource.Ticket, stored.Source);
        Assert.Equal("t1", stored.SourceId);
        tickets.Verify(service => service.LinkAsync("t1", new RecordRef(WorkOrdersConstants.RecordKind, stored.ItemId), TicketLinkKinds.Related, It.IsAny<TicketActor>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Null(refused.WorkOrder);
        Assert.NotEmpty(refused.Errors);
    }

    [Theory]
    [InlineData(WorkOrdersConstants.Events.Completed, WorkOrderStatus.Completed, "Work order WO-7 was completed.")]
    [InlineData(WorkOrdersConstants.Events.StatusChanged, WorkOrderStatus.Canceled, "Work order WO-7 was canceled.")]
    [InlineData(WorkOrdersConstants.Events.StatusChanged, WorkOrderStatus.Scheduled, null)]
    [InlineData(WorkOrdersConstants.Events.Created, WorkOrderStatus.New, null)]
    public async Task TheTicket_HearsWhenItsWorkOrderIsDone(string eventName, WorkOrderStatus status, string expected)
    {
        // Arrange
        var added = new List<TimelineEntry>();
        var timeline = new Mock<ITimelineService>();
        timeline.Setup(service => service.AddAsync(It.IsAny<TimelineEntry>(), It.IsAny<CancellationToken>()))
            .Callback((TimelineEntry entry, CancellationToken _) => added.Add(entry))
            .ReturnsAsync(new TimelineResult());
        var observer = new TicketWorkOrderObserver(timeline.Object, Localizer<TicketWorkOrderObserver>());
        var fromTicket = new WorkOrder { Number = "WO-7", Status = status, Source = WorkOrderSource.Ticket, SourceId = "t1" };
        var manual = new WorkOrder { Number = "WO-8", Status = status, Source = WorkOrderSource.Manual };

        // Act
        await observer.OnEventAsync(fromTicket, eventName, new Dictionary<string, object>(), TestContext.Current.CancellationToken);
        await observer.OnEventAsync(manual, eventName, new Dictionary<string, object>(), TestContext.Current.CancellationToken);

        // Assert
        if (expected is null)
        {
            Assert.Empty(added);

            return;
        }

        var entry = Assert.Single(added);
        Assert.Equal(new RecordRef(TicketsConstants.RecordKind, "t1"), entry.Subject);
        Assert.Equal(TicketsConstants.EntryKinds.Event, entry.Kind);
        Assert.Equal(TimelineVisibility.Internal, entry.Visibility);
        Assert.Equal(expected, entry.Body);
    }

    private static ContentItem Ticket(string id, string subject)
    {
        var ticket = new ContentItem { ContentItemId = id, ContentType = "Ticket", DisplayText = subject };
        ticket.Apply(new TicketPart
        {
            Number = "T-1",
            AccountId = "acme",
            Requester = new PartyRef(PartyKinds.Contact, "contact-1"),
            RequesterContact = new ContactInfo
            {
                Name = "Dana",
                Email = "dana@contoso.com",
                Phone = PhoneNumber.FromE164("+17025550100"),
                Address = new Address { City = "Las Vegas" },
            },
        });

        return ticket;
    }

    private static TimelineEntry Reply(string body, DateTime createdUtc) => new()
    {
        Subject = new RecordRef(TicketsConstants.RecordKind, "t1"),
        Kind = TicketsConstants.EntryKinds.Reply,
        Visibility = TimelineVisibility.Public,
        AuthorName = "Dana",
        Body = body,
        CreatedUtc = createdUtc,
    };

    private static IStringLocalizer<T> Localizer<T>()
    {
        var localizer = new Mock<IStringLocalizer<T>>();
        localizer.Setup(value => value[It.IsAny<string>()]).Returns((string name) => new LocalizedString(name, name));
        localizer.Setup(value => value[It.IsAny<string>(), It.IsAny<object[]>()]).Returns((string name, object[] arguments) => new LocalizedString(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments)));

        return localizer.Object;
    }
}
