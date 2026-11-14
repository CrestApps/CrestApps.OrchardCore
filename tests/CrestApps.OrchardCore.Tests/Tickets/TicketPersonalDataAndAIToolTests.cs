using System.Security.Claims;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.PersonalData;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tickets;
using CrestApps.OrchardCore.Tickets.AI;
using CrestApps.OrchardCore.Tickets.Core.Indexes;
using CrestApps.OrchardCore.Tickets.Integrations;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using CrestApps.OrchardCore.Timeline.Models;
using CrestApps.OrchardCore.Timeline.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.ContentManagement;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Tickets;

/// <summary>
/// A person's tickets in a personal data request are found by their user and by the e-mail kept on a guest's ticket;
/// an erasure removes their contact details and rewrites the subject and the timeline so nothing names them. The ticket
/// AI tools act only for a signed-in user and only within what that user may do.
/// </summary>
public sealed class TicketPersonalDataAndAIToolTests
{
    [Fact]
    public async Task ATicketsRequester_IsFoundByUserAndByEmail_AndErasureRemovesWhatNamesThem()
    {
        // Arrange
        var timeline = new InMemoryTimelineService();
        await using var harness = await CalendarTestHarness.CreateAsync(
            services =>
            {
                services.AddSingleton<ITimelineService>(timeline);
                services.AddScoped<IContentManager>(provider =>
                {
                    var session = provider.GetRequiredService<ISession>();
                    var contents = new Mock<IContentManager>();
                    contents.Setup(manager => manager.UpdateAsync(It.IsAny<ContentItem>())).Returns((ContentItem item) => session.SaveAsync(item));

                    return contents.Object;
                });
                services.AddScoped<TicketPersonalDataContributor>();
            },
            TicketIndexSchema.CreateAsync,
            [new TicketIndexProvider()]);

        await harness.RunAsync(async services =>
        {
            var session = services.GetRequiredService<ISession>();
            await session.SaveAsync(Ticket("t1", "Pat Doe can't print", new PartyRef(PartyKinds.User, "u1"), null));
            await session.SaveAsync(Ticket("t2", "Scanner offline", default, new ContactInfo { Name = "Pat Doe", Email = "Pat@Example.com" }));
            await session.SaveAsync(Ticket("t3", "Someone else", default, new ContactInfo { Name = "Lee", Email = "lee@example.com" }));
        });
        timeline.Entries.Add(new TimelineEntry { ItemId = "e1", Subject = new RecordRef(TicketsConstants.RecordKind, "t2"), Kind = TicketsConstants.EntryKinds.Reply, Body = "Hi Pat Doe, try again.", AuthorName = "Agent" });

        // Act
        var subject = new PersonalDataSubject("u1", "pat@example.com");
        var export = new PersonalDataExport();
        var result = new PersonalDataAnonymizeResult();
        await harness.RunAsync(async services =>
        {
            var contributor = services.GetRequiredService<TicketPersonalDataContributor>();
            await contributor.IdentifyAsync(subject, TestContext.Current.CancellationToken);
            await contributor.ExportAsync(new PersonalDataExportContext(subject, export), TestContext.Current.CancellationToken);
            await contributor.AnonymizeAsync(new PersonalDataAnonymizeContext(subject, "[removed]", result), TestContext.Current.CancellationToken);
        });
        var t2 = await harness.RunAsync(services => services.GetRequiredService<ISession>().Query<ContentItem, TicketIndex>(index => index.ContentItemId == "t2").FirstOrDefaultAsync(), commit: false);

        // Assert
        Assert.Equal(["t1", "t2"], export.Records.Where(record => record.Kind == TicketPersonalDataContributor.TicketKind).Select(record => record.Id).Order());
        Assert.Contains(export.Records, record => record.Kind == TicketPersonalDataContributor.ReplyKind && record.Id == "e1");
        Assert.Equal(2, result.Records[TicketPersonalDataContributor.TicketKind]);
        t2.TryGet<TicketPart>(out var part);
        var contact = part.RequesterContact;
        Assert.Equal("[removed]", contact.Name);
        Assert.Null(contact.Email);
        Assert.Equal("Hi [removed], try again.", timeline.Entries.Single().Body);
    }

    [Fact]
    public async Task TheTools_ActOnlyForASignedInUser_AndWithinWhatTheyMayDo()
    {
        // Arrange
        var ticket = Ticket("t1", "Printer jams", default, null);
        var tickets = new Mock<ITicketService>();
        tickets.Setup(service => service.GetAsync("t1")).ReturnsAsync(ticket);
        var access = new Mock<ITicketAccessService>();
        access.Setup(service => service.CanAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<ContentItem>(), It.IsAny<TicketOperation>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClaimsPrincipal _, ContentItem _, TicketOperation operation, CancellationToken _) => operation is TicketOperation.ViewAssigned or TicketOperation.Edit);
        HttpContext context = null;
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.SetupGet(value => value.HttpContext).Returns(() => context);
        var services = new ServiceCollection()
            .AddSingleton(tickets.Object)
            .AddSingleton(access.Object)
            .AddSingleton(accessor.Object)
            .BuildServiceProvider();
        var tool = new UpdateTicketTool();

        // Act
        var anonymous = await tool.InvokeAsync(Arguments(services, ("ticket", "t1"), ("assigneeId", "ben")), TestContext.Current.CancellationToken);
        context = new DefaultHttpContext { User = CalendarTestHarness.User("ana") };
        var assign = await tool.InvokeAsync(Arguments(services, ("ticket", "t1"), ("assigneeId", "ben")), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("Only a signed-in user can work with tickets.", anonymous?.ToString());
        Assert.Equal("The user may not assign this ticket.", assign?.ToString());
        tickets.Verify(service => service.UpdateAsync(It.IsAny<string>(), It.IsAny<TicketUpdate>(), It.IsAny<TicketActor>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static AIFunctionArguments Arguments(IServiceProvider services, params (string Name, object Value)[] values)
        => new(values.ToDictionary(value => value.Name, value => value.Value)) { Services = services };

    private static ContentItem Ticket(string id, string subject, PartyRef requester, ContactInfo contact)
    {
        var item = new ContentItem { ContentItemId = id, ContentType = TicketsConstants.DefaultContentType, Latest = true, Published = true, DisplayText = subject };
        item.Apply(new TicketPart { Number = id, DeskId = "support", Requester = requester, RequesterContact = contact });

        return item;
    }
}
