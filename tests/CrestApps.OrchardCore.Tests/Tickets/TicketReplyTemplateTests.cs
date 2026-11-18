using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tickets;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Tickets;

/// <summary>
/// The reply templates a ticket's composer offers: Messaging's templates of the Tickets scope, filled in with the ticket
/// and its requester, with what using one changes on the ticket read from its properties; a template of another scope
/// isn't found for a ticket.
/// </summary>
public sealed class TicketReplyTemplateTests
{
    [Fact]
    public async Task TemplatesOfTheTicketsScope_AreFilledInForTheTicket_WithTheirChanges()
    {
        // Arrange
        var macro = new MessageTemplate
        {
            ItemId = "m1",
            Name = "Solved: restart",
            Scope = TicketsConstants.ReplyTemplateScope,
            FillsPlaceholders = true,
            Body = "Hi {{ Contact.DisplayName }}, {{ Ticket.Number }} is solved.",
        };
        macro.Properties[TicketReplyTemplate.PropertyKeys.StatusId] = "solved";
        macro.Properties[TicketReplyTemplate.PropertyKeys.PriorityId] = "low";
        var other = new MessageTemplate { ItemId = "w1", Name = "Workspace", Body = "Hello" };

        var manager = new Mock<IMessageTemplateManager>();
        manager.Setup(value => value.ListByScopeAsync(TicketsConstants.ReplyTemplateScope, It.IsAny<CancellationToken>())).ReturnsAsync([macro]);
        manager.Setup(value => value.FindByIdAsync("m1", It.IsAny<CancellationToken>())).ReturnsAsync(macro);
        manager.Setup(value => value.FindByIdAsync("w1", It.IsAny<CancellationToken>())).ReturnsAsync(other);

        var renderer = new LiquidMessageTemplateRenderer(LiquidTestClocks.In("UTC"), RecipeRoundTrip.Localizer<LiquidMessageTemplateRenderer>(), NullLogger<LiquidMessageTemplateRenderer>.Instance);
        var source = new MessagingTicketReplyTemplateSource(manager.Object, renderer);

        var ticket = new ContentItem { ContentItemId = "t1", DisplayText = "Printer jams" };
        ticket.Apply(new TicketPart { Number = "T-7", RequesterContact = new ContactInfo { Name = "Dana" } });

        // Act
        var listed = Assert.Single(await source.ListAsync(ticket, TestContext.Current.CancellationToken));
        var workspace = await source.FindAsync("w1", ticket, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("Hi Dana, T-7 is solved.", listed.Body);
        Assert.Equal("solved", listed.StatusId);
        Assert.Equal("low", listed.PriorityId);
        Assert.Null(listed.TeamId);
        Assert.Null(workspace);
    }
}
