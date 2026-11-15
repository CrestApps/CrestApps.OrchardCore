using CrestApps.Core.Services;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Attachments.Models;
using CrestApps.OrchardCore.Attachments.Services;
using CrestApps.OrchardCore.Core.Emails;
using CrestApps.OrchardCore.Emails;
using CrestApps.OrchardCore.Parties.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tickets;
using CrestApps.OrchardCore.Tickets.Core.Services;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Timeline.Models;
using CrestApps.OrchardCore.Timeline.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Entities;
using OrchardCore.Localization;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Tickets;

/// <summary>
/// The e-mails requesters get: received, an agent's public reply, and solved, each once, on a desk that e-mails
/// requesters; never a note, the requester's own words, or anything on a desk that doesn't e-mail. The site's templates
/// answer only for ticket kinds, and the shared editor keeps the templates of kinds no feature registers now and refuses
/// Liquid that can't be used.
/// </summary>
public sealed class TicketEmailTests
{
    private readonly List<(TemplatedEmail Email, TemplatedEmailContent Content)> _sent = [];

    [Fact]
    public async Task TheRequesterGetsReceived_AnAgentsPublicReply_AndSolvedOnce()
    {
        // Arrange
        var observer = Observer(notify: true);
        var ticket = Ticket();
        var solved = new TicketStatus { ItemId = "solved", Name = "Solved", CustomerLabel = "Done", Category = TicketStatusCategory.Solved };
        var open = new TicketStatus { ItemId = "open", Name = "Open", Category = TicketStatusCategory.Open };

        // Act
        await observer.CreatedAsync(ticket, TestContext.Current.CancellationToken);
        await observer.RepliedAsync(ticket, Entry("Try the rear tray.\n\nThen restart it.", TimelineVisibility.Public), fromRequester: false, TestContext.Current.CancellationToken);
        await observer.RepliedAsync(ticket, Entry("Known issue.", TimelineVisibility.Internal), fromRequester: false, TestContext.Current.CancellationToken);
        await observer.RepliedAsync(ticket, Entry("Still broken.", TimelineVisibility.Public), fromRequester: true, TestContext.Current.CancellationToken);
        await observer.StatusChangedAsync(ticket, open, solved, TestContext.Current.CancellationToken);
        await observer.StatusChangedAsync(ticket, solved, solved, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([TicketsConstants.EmailKinds.Received, TicketsConstants.EmailKinds.Reply, TicketsConstants.EmailKinds.Solved], _sent.Select(sent => sent.Email.Kind));
        Assert.All(_sent, sent => Assert.Equal("dana@example.com", sent.Email.To));
        Assert.Contains("<p>Try the rear tray.</p><p>Then restart it.</p>", _sent[1].Content.Html);
        Assert.Equal("Try the rear tray.\n\nThen restart it.", ((TicketEmailReply)_sent[1].Email.Models["Reply"]).Body);
        Assert.Equal("Done", ((TicketEmailModel)_sent[2].Email.Models["Ticket"]).Status);
    }

    [Fact]
    public async Task AReplysFiles_AreLinkedFromTheSitesAddress_AndTemplatesGetTheReplyAsHtml()
    {
        // Arrange
        var attachments = new Mock<IAttachmentService>();
        attachments.Setup(service => service.GetAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Attachment { ItemId = "f1", FileName = "log <1>.txt" }]);
        attachments.Setup(service => service.CreateSignedPath("f1", TimeSpan.FromDays(30))).Returns("/acme/attachments/f1?token=abc");
        var observer = Observer(notify: true, attachments.Object, baseUrl: "https://help.example.com/acme");
        var entry = Entry("See the log.", TimelineVisibility.Public);
        entry.AttachmentIds = ["f1"];

        // Act
        await observer.RepliedAsync(Ticket(), entry, fromRequester: false, TestContext.Current.CancellationToken);

        // Assert
        var reply = (TicketEmailReply)Assert.Single(_sent).Email.Models["Reply"];
        Assert.Equal("<p>See the log.</p><p>Files:</p><ul><li><a href=\"https://help.example.com/acme/attachments/f1?token=abc\">log &lt;1&gt;.txt</a></li></ul>", reply.Html);
        Assert.StartsWith(reply.Html, _sent[0].Content.Html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ADeskThatDoesntEmailRequesters_SendsNothing()
    {
        // Arrange
        var observer = Observer(notify: false);

        // Act
        await observer.CreatedAsync(Ticket(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(_sent);
    }

    [Fact]
    public async Task TheSitesTemplates_AnswerOnlyForTicketKinds()
    {
        // Arrange
        var site = new SiteSettings();
        site.Put(new TicketEmailSettings
        {
            Layout = "<div>{{ Content }}</div>",
            Templates = { [TicketsConstants.EmailKinds.Reply] = new TemplatedEmailTemplate { IsEnabled = true, Body = "Hi {{ Ticket.RequesterName }}" } },
        });
        var siteService = new Mock<ISiteService>();
        siteService.Setup(service => service.GetSiteSettingsAsync()).ReturnsAsync(site);
        var options = new TemplatedEmailOptions()
            .AddKind(new TemplatedEmailKind(TicketsConstants.EmailGroup, TicketsConstants.EmailKinds.Reply, LocalizationSource.Create<TicketEmailTests>("Reply")))
            .AddKind(new TemplatedEmailKind("Commerce", "order-confirmation", LocalizationSource.Create<TicketEmailTests>("Order")));
        var source = new TicketEmailTemplateSource(siteService.Object, Options.Create(options));

        // Act
        var reply = await source.FindAsync(TicketsConstants.EmailKinds.Reply, TestContext.Current.CancellationToken);
        var order = await source.FindAsync("order-confirmation", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("Hi {{ Ticket.RequesterName }}", reply.Template.Body);
        Assert.Equal("<div>{{ Content }}</div>", reply.Layout);
        Assert.Null(order);
    }

    [Fact]
    public void TheSharedEditor_KeepsTemplatesOfKindsNotRegisteredNow_AndRefusesLiquidThatCantBeUsed()
    {
        // Arrange
        var kinds = new[] { new TemplatedEmailKind(TicketsConstants.EmailGroup, TicketsConstants.EmailKinds.Reply, LocalizationSource.Create<TicketEmailTests>("Reply")) };
        var kept = new Dictionary<string, TemplatedEmailTemplate>
        {
            ["ticket-rating"] = new() { IsEnabled = true, Body = "Rate us" },
        };
        var model = new TemplatedEmailTemplatesEditorViewModel();
        TemplatedEmailTemplatesEditor.Build(model, kinds, kept, layout: null, new PassThroughStringLocalizerFactory());
        var defaultBody = model.Templates[0].Body;
        model.Templates[0].IsEnabled = true;
        model.Templates[0].Body = "{{ Reply.Body | markdownify }}";

        // Act
        var edit = TemplatedEmailTemplatesEditor.Apply(model, kinds, kept);

        // Assert
        Assert.Equal("{{ Content }}", defaultBody);
        Assert.Equal("Rate us", edit.Templates["ticket-rating"].Body);
        Assert.True(edit.Templates[TicketsConstants.EmailKinds.Reply].IsEnabled);
        var error = Assert.Single(edit.Errors);
        Assert.Equal("Templates[0].Body", error.Key);
    }

    private TicketEmailObserver Observer(bool notify, IAttachmentService attachments = null, string baseUrl = null)
    {
        var desks = new Mock<ICatalog<TicketDesk>>();
        desks.Setup(catalog => catalog.FindByIdAsync("support", It.IsAny<CancellationToken>())).ReturnsAsync(new TicketDesk { ItemId = "support", NotifyRequester = notify });

        var sender = new Mock<ITemplatedEmailSender>();
        sender.Setup(value => value.SendAsync(It.IsAny<TemplatedEmail>(), It.IsAny<Func<CancellationToken, Task<TemplatedEmailContent>>>(), It.IsAny<CancellationToken>()))
            .Returns(async (TemplatedEmail email, Func<CancellationToken, Task<TemplatedEmailContent>> build, CancellationToken token) =>
            {
                _sent.Add((email, await build(token)));

                return true;
            });

        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(new DateTime(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc));
        var site = new Mock<ISiteService>();
        site.Setup(service => service.GetSiteSettingsAsync()).ReturnsAsync(new SiteSettings { BaseUrl = baseUrl });
        var services = new ServiceCollection()
            .AddSingleton(sender.Object)
            .AddSingleton(Mock.Of<global::YesSql.ISession>())
            .AddSingleton(Mock.Of<ITimelineService>())
            .AddSingleton(clock.Object)
            .AddSingleton(site.Object)
            .AddSingleton(RecipeRoundTrip.Localizer<TicketEmailOutbox>())
            .AddSingleton<TicketEmailOutbox>();

        if (attachments is not null)
        {
            services.AddSingleton(attachments);
        }

        return new TicketEmailObserver(desks.Object, Mock.Of<IPartyDirectory>(), services.BuildServiceProvider(), RecipeRoundTrip.Localizer<TicketEmailObserver>());
    }

    private static ContentItem Ticket()
    {
        var ticket = new ContentItem { ContentItemId = "t1", ContentType = TicketsConstants.DefaultContentType, DisplayText = "Printer jams" };
        ticket.Apply(new TicketPart { Number = "T-1", DeskId = "support", RequesterContact = new ContactInfo { Name = "Dana", Email = "dana@example.com" } });

        return ticket;
    }

    private static TimelineEntry Entry(string body, TimelineVisibility visibility)
        => new() { ItemId = "e1", Kind = "Reply", Visibility = visibility, Body = body, AuthorName = "Ana" };

    private sealed class PassThroughStringLocalizerFactory : Microsoft.Extensions.Localization.IStringLocalizerFactory
    {
        public Microsoft.Extensions.Localization.IStringLocalizer Create(Type resourceSource)
            => RecipeRoundTrip.Localizer<TicketEmailTests>();

        public Microsoft.Extensions.Localization.IStringLocalizer Create(string baseName, string location)
            => RecipeRoundTrip.Localizer<TicketEmailTests>();
    }
}
