using CrestApps.Core.Services;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Emails;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Parties.Services;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Tickets;
using CrestApps.OrchardCore.Tickets.Core.Indexes;
using CrestApps.OrchardCore.Tickets.Core.Services;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Notifications;
using CrestApps.OrchardCore.Tickets.Services;
using CrestApps.OrchardCore.Timeline.Models;
using CrestApps.OrchardCore.Timeline.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Entities;
using OrchardCore.Modules;
using OrchardCore.Notifications.Models;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Tickets;

/// <summary>
/// The messages requesters get (received, an agent's reply, solved, the rating request) are notification templates: a
/// requester with a site account gets each through Orchard Core's Notifications (the notification center and the methods
/// they chose), with a notification type per kind and the template the site wrote; a requester without an account, or
/// one whose account is gone, gets the same message by e-mail.
/// </summary>
public sealed class TicketRequesterNotificationTests
{
    [Fact]
    public async Task ARequesterWithAnAccount_GetsTheTemplatedMessageAsANotification_AndNoEmail()
    {
        // Arrange
        var notifier = new FakeNotifier();
        await using var kit = await TicketKit.CreateAsync(configureServices: services => services
            .AddSingleton<ITicketRequesterNotifier>(notifier)
            .AddSingleton<ITemplatedEmailTemplates>(new WrappingTemplates()));
        var item = Item(userId: "u-dana", to: "dana@example.com");

        // Act
        var sent = await kit.RunAsync(services => services.GetRequiredService<TicketEmailOutbox>().SendAsync(item, TestContext.Current.CancellationToken));

        // Assert: the site's template made the message, and the e-mail service wasn't used.
        Assert.True(sent);
        var (userId, notified, message) = Assert.Single(notifier.Notified);
        Assert.Equal(("u-dana", TicketsConstants.EmailKinds.Reply), (userId, notified.Kind));
        Assert.Equal(("Template: [T-1] Printer jams", "<div class=\"template\"><p>Hello</p></div>"), (message.Subject, message.Html));
        Assert.Empty(kit.Sender.Sent);
        Assert.Null(await kit.ReadAsync(services => services.GetRequiredService<ISession>().Query<TicketEmailOutboxItem>(TicketsConstants.CollectionName).FirstOrDefaultAsync(TestContext.Current.CancellationToken)));
    }

    [Fact]
    public async Task ARequesterWhoseAccountIsGone_GetsTheMessageByEmail()
    {
        // Arrange
        var notifier = new FakeNotifier { UserExists = false };
        await using var kit = await TicketKit.CreateAsync(configureServices: services => services.AddSingleton<ITicketRequesterNotifier>(notifier));

        // Act
        var sent = await kit.RunAsync(services => services.GetRequiredService<TicketEmailOutbox>().SendAsync(Item(userId: "u-gone", to: "dana@example.com"), TestContext.Current.CancellationToken));

        // Assert
        Assert.True(sent);
        Assert.Equal("dana@example.com", Assert.Single(kit.Sender.Sent).Email.To);
    }

    [Fact]
    public async Task ARequesterWithoutAnAccount_GetsTheMessageByEmail()
    {
        // Arrange
        var notifier = new FakeNotifier();
        await using var kit = await TicketKit.CreateAsync(configureServices: services => services.AddSingleton<ITicketRequesterNotifier>(notifier));

        // Act
        await kit.RunAsync(services => services.GetRequiredService<TicketEmailOutbox>().SendAsync(Item(userId: null, to: "dana@example.com"), TestContext.Current.CancellationToken));

        // Assert
        Assert.Empty(notifier.Notified);
        Assert.Equal("dana@example.com", Assert.Single(kit.Sender.Sent).Email.To);
    }

    [Fact]
    public async Task TheRequesterMessages_NameTheRequestersAccount_EvenWithoutAnEmailAddress()
    {
        // Arrange: Dana signs in and has no e-mail address on the ticket or in the directory; Lee is a guest.
        var notifier = new FakeNotifier();
        var emails = new List<TemplatedEmail>();
        var observer = Observer(notifier, emails);
        var dana = Ticket("t1", new PartyRef(PartyKinds.User, "u-dana"), email: null);
        var lee = Ticket("t2", default, email: "lee@example.com");

        // Act
        await observer.CreatedAsync(dana, TestContext.Current.CancellationToken);
        await observer.RepliedAsync(dana, new TimelineEntry { ItemId = "e1", Kind = TicketsConstants.EntryKinds.Reply, Visibility = TimelineVisibility.Public, Body = "Try the rear tray.", AuthorName = "Ana" }, fromRequester: false, TestContext.Current.CancellationToken);
        await observer.CreatedAsync(lee, TestContext.Current.CancellationToken);

        // Assert: Dana hears through her account, Lee by e-mail.
        Assert.Equal(
            [("t1", TicketsConstants.EmailKinds.Received, "u-dana", (string)null), ("t1", TicketsConstants.EmailKinds.Reply, "u-dana", null)],
            notifier.Notified.Select(notified => (notified.Item.TicketId, notified.Item.Kind, notified.UserId, notified.Item.To)));
        var email = Assert.Single(emails);
        Assert.Equal((TicketsConstants.EmailKinds.Received, "lee@example.com"), (email.Kind, email.To));
        Assert.Equal(new RecordRef(TicketsConstants.RecordKind, "t2"), email.Source);
    }

    [Theory]
    [InlineData(TicketsConstants.EmailKinds.Received, TicketsConstants.NotificationTypes.RequestReceived, "~/account/requests/T-1")]
    [InlineData(TicketsConstants.EmailKinds.Reply, TicketsConstants.NotificationTypes.Replied, "~/account/requests/T-1")]
    [InlineData(TicketsConstants.EmailKinds.Solved, TicketsConstants.NotificationTypes.RequestSolved, "~/account/requests/T-1")]
    [InlineData(TicketsConstants.EmailKinds.Rating, TicketsConstants.NotificationTypes.RatingRequested, "https://example.com/account/requests/T-1/rate")]
    public async Task EachKindOfMessage_HasItsOwnNotificationType_AndOpensTheRequestOnThisSite(string kind, string type, string url)
    {
        // Arrange
        var (sender, sent) = TicketNotificationTypesTests.Sender();
        var notifier = new TicketRequesterNotifier(sender, new PassThroughStringLocalizer<TicketRequesterNotifier>());
        var item = Item(userId: "u-dana", to: null);
        item.Kind = kind;
        item.TicketLink = kind == TicketsConstants.EmailKinds.Rating ? "https://example.com/account/requests/T-1/rate" : "https://example.com/account/requests/T-1";

        // Act
        var notified = await notifier.NotifyAsync("u-dana", item, new TemplatedEmailMessage("Subject", "Text", "<p>Html</p>"), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(notified);
        var (to, message) = Assert.Single(sent);
        // The signed-in requester opens their request on the site they are on (the e-mail's link names the site's address);
        // a rating request opens its rating page.
        Assert.Equal(("u-dana", type, "t1", url), (to, message.Type, message.ReferenceId, message.Url));
        Assert.Equal(("Subject", "Text", "<p>Html</p>", true), (message.Subject, message.TextBody, message.HtmlBody, message.IsHtmlPreferred));
        Assert.False(string.IsNullOrEmpty(message.Summary));
        Assert.Equal("T-1", ((NotificationMessage)message).GetOrCreate<TicketNotificationInfo>().Number);
    }

    [Fact]
    public async Task AMessageWithoutALink_OpensTheRequestInTheRequestersAccount()
    {
        // Arrange
        var (sender, sent) = TicketNotificationTypesTests.Sender();
        var notifier = new TicketRequesterNotifier(sender, new PassThroughStringLocalizer<TicketRequesterNotifier>());

        // Act
        await notifier.NotifyAsync("u-dana", Item(userId: "u-dana", to: null), new TemplatedEmailMessage("Subject", "Text", "<p>Html</p>"), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("~/account/requests/T-1", Assert.Single(sent).Message.Url);
    }

    private static TicketEmailOutboxItem Item(string userId, string to)
        => new()
        {
            TicketId = "t1",
            Kind = TicketsConstants.EmailKinds.Reply,
            To = to,
            RecipientUserId = userId,
            Reference = "T-1",
            Subject = "[T-1] Printer jams",
            Html = "<p>Hello</p>",
            TicketNumber = "T-1",
            TicketSubject = "Printer jams",
            ReplyBody = "Hello",
            ReplyHtml = "<p>Hello</p>",
            ReplyAuthorName = "Ana",
        };

    private static ContentItem Ticket(string id, PartyRef requester, string email)
    {
        var ticket = new ContentItem { ContentItemId = id, ContentType = TicketsConstants.DefaultContentType, DisplayText = "Printer jams" };
        ticket.Apply(new TicketPart { Number = "T-" + id, DeskId = "support", Requester = requester, RequesterContact = email is null ? null : new ContactInfo { Name = "Lee", Email = email } });

        return ticket;
    }

    // The observer with a notifier and an outbox that only keep what they are given.
    private static TicketEmailObserver Observer(FakeNotifier notifier, List<TemplatedEmail> emails)
    {
        var desks = new Mock<ICatalog<TicketDesk>>();
        desks.Setup(catalog => catalog.FindByIdAsync("support", It.IsAny<CancellationToken>())).ReturnsAsync(new TicketDesk { ItemId = "support", NotifyRequester = true });
        var outbox = new Mock<ITemplatedEmailOutbox>();
        outbox.Setup(value => value.SendAsync(It.IsAny<TemplatedEmail>(), It.IsAny<Func<CancellationToken, Task<TemplatedEmailContent>>>(), It.IsAny<CancellationToken>()))
            .Callback((TemplatedEmail email, Func<CancellationToken, Task<TemplatedEmailContent>> _, CancellationToken _) => emails.Add(email))
            .ReturnsAsync(new TemplatedEmailOutboxResult { ItemId = "email-1", Status = TemplatedEmailOutboxStatus.Sent });
        var services = new ServiceCollection()
            .AddSingleton(outbox.Object)
            .AddSingleton(Mock.Of<ISession>())
            .AddSingleton(Mock.Of<ITimelineService>())
            .AddSingleton<ITicketRequesterNotifier>(notifier)
            .AddSingleton(RecipeRoundTrip.Localizer<TicketEmailOutbox>())
            .AddLogging()
            .AddSingleton<TicketEmailOutbox>();

        return new TicketEmailObserver(desks.Object, Mock.Of<IPartyDirectory>(), services.BuildServiceProvider(), RecipeRoundTrip.Localizer<TicketEmailObserver>());
    }

    private sealed class FakeNotifier : ITicketRequesterNotifier
    {
        public bool UserExists { get; init; } = true;

        public List<(string UserId, TicketEmailOutboxItem Item, TemplatedEmailMessage Message)> Notified { get; } = [];

        public Task<bool> NotifyAsync(string userId, TicketEmailOutboxItem item, TemplatedEmailMessage message, CancellationToken cancellationToken = default)
        {
            if (!UserExists)
            {
                return Task.FromResult(false);
            }

            Notified.Add((userId, item, message));

            return Task.FromResult(true);
        }
    }

    // The site's template: its own subject and a wrapper around the message.
    private sealed class WrappingTemplates : ITemplatedEmailTemplates
    {
        public Task<TemplatedEmailMessage> ApplyAsync(TemplatedEmailContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(new TemplatedEmailMessage("Template: " + context.Subject, context.Text, "<div class=\"template\">" + context.Html + "</div>"));
    }
}
