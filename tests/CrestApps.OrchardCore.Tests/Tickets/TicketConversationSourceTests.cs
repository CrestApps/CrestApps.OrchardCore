using System.Security.Claims;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tickets.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;
using YesSql;
using YesSql.Indexes;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Tickets;

/// <summary>
/// A messaging conversation gives a new ticket its customer, a subject and its latest messages, oldest first, with who
/// wrote each; only to an agent who may read the conversation.
/// </summary>
public sealed class TicketConversationSourceTests
{
    [Fact]
    public async Task AConversation_GivesItsCustomerSubjectAndMessages_ToAnAgentWhoMayReadIt()
    {
        // Arrange
        var allowed = true;
        await using var harness = await CreateAsync(() => allowed);
        await harness.RunAsync(async services =>
        {
            var session = services.GetRequiredService<ISession>();
            await session.SaveAsync(Message("m1", "c1", inbound: true, "My order hasn't arrived.", CalendarTestHarness.Now.AddMinutes(-10)), false, OmnichannelConstants.CollectionName);
            await session.SaveAsync(Message("m2", "c1", inbound: false, "Let me check.", CalendarTestHarness.Now.AddMinutes(-5)), false, OmnichannelConstants.CollectionName);
            await session.SaveAsync(Message("m3", "other", inbound: true, "Not this one.", CalendarTestHarness.Now.AddMinutes(-1)), false, OmnichannelConstants.CollectionName);
        });

        // Act
        var seed = await harness.RunAsync(services => services.GetRequiredService<TicketConversationSource>().GetAsync(CalendarTestHarness.User("ana"), "c1", TestContext.Current.CancellationToken), commit: false);
        allowed = false;
        var refused = await harness.RunAsync(services => services.GetRequiredService<TicketConversationSource>().GetAsync(CalendarTestHarness.User("ben"), "c1", TestContext.Current.CancellationToken), commit: false);

        // Assert
        Assert.Equal(new PartyRef(PartyKinds.Contact, "contact-1"), seed.Requester);
        Assert.Equal("Dana Reyes", seed.CustomerName);
        Assert.Contains("Dana Reyes", seed.Subject, StringComparison.Ordinal);
        Assert.True(seed.Body.IndexOf("My order hasn't arrived.", StringComparison.Ordinal) < seed.Body.IndexOf("Let me check.", StringComparison.Ordinal));
        Assert.Contains("Dana Reyes: My order", seed.Body, StringComparison.Ordinal);
        Assert.Contains("Agent: Let me check.", seed.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("Not this one.", seed.Body, StringComparison.Ordinal);
        Assert.Null(refused);
    }

    private static Task<CalendarTestHarness> CreateAsync(Func<bool> allowed)
        => CalendarTestHarness.CreateAsync(
            services =>
            {
                var conversations = new Mock<IMessagingConversationStore>();
                conversations.Setup(store => store.FindByIdAsync("c1", It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new MessagingConversation { ItemId = "c1", Channel = "Sms", ContactAddress = "+17025550100", ContactContentItemId = "contact-1" });
                services.AddSingleton(conversations.Object);

                var authorization = new Mock<IAuthorizationService>();
                authorization.Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
                    .ReturnsAsync(() => allowed() ? AuthorizationResult.Success() : AuthorizationResult.Failed());
                services.AddSingleton(authorization.Object);

                var contents = new Mock<IContentManager>();
                contents.Setup(manager => manager.GetAsync("contact-1", It.IsAny<VersionOptions>()))
                    .ReturnsAsync(new ContentItem { ContentItemId = "contact-1", DisplayText = "Dana Reyes" });
                services.AddSingleton(contents.Object);

                var clock = new Mock<ILocalClock>();
                clock.Setup(value => value.ConvertToLocalAsync(It.IsAny<DateTimeOffset>())).ReturnsAsync((DateTimeOffset value) => value);
                services.AddSingleton(clock.Object);

                services.AddSingleton(RecipeRoundTrip.Localizer<TicketConversationSource>());
                services.AddScoped<TicketConversationSource>();
            },
            schema => schema.CreateMapIndexTableAsync<OmnichannelMessageIndex>(table => table
                .Column<string>("Channel", column => column.WithLength(50))
                .Column<string>("CustomerAddress", column => column.WithLength(255))
                .Column<string>("ServiceAddress", column => column.WithLength(255))
                .Column<DateTime>("CreatedUtc", column => column.NotNull())
                .Column<bool>("IsInbound", column => column.NotNull().WithDefault(false))
                .Column<string>("ConversationId", column => column.WithLength(26))
                .Column<string>("ProviderMessageId", column => column.WithLength(128)),
                collection: OmnichannelConstants.CollectionName),
            [new MessageIndexProvider()],
            [OmnichannelConstants.CollectionName]);

    private static OmnichannelMessage Message(string id, string conversationId, bool inbound, string content, DateTime createdUtc)
        => new() { Id = id, ConversationId = conversationId, IsInbound = inbound, Content = content, CreatedUtc = createdUtc, Channel = "Sms" };

    private sealed class MessageIndexProvider : IndexProvider<OmnichannelMessage>
    {
        public MessageIndexProvider()
        {
            CollectionName = OmnichannelConstants.CollectionName;
        }

        public override void Describe(DescribeContext<OmnichannelMessage> context)
            => context.For<OmnichannelMessageIndex>().Map(message => new OmnichannelMessageIndex
            {
                Channel = message.Channel,
                CreatedUtc = message.CreatedUtc,
                IsInbound = message.IsInbound,
                ConversationId = message.ConversationId,
            });
    }
}
