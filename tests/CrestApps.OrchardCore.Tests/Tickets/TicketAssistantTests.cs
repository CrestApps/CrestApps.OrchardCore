using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Tickets;
using CrestApps.OrchardCore.Tickets.AI;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using CrestApps.OrchardCore.Timeline.Models;
using CrestApps.OrchardCore.Timeline.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Tickets;

/// <summary>
/// The desk's AI assistant reads the ticket's conversation (internal notes marked as such), suggests a reply the agent
/// sends, and triages into an internal note with a summary and a priority, setting it only when the desk says so.
/// </summary>
public sealed class TicketAssistantTests
{
    private static readonly TicketPriority _normal = new() { ItemId = "normal", Name = "Normal", Rank = 2 };
    private static readonly TicketPriority _urgent = new() { ItemId = "urgent", Name = "Urgent", Rank = 4 };

    [Theory]
    [InlineData("{\"summary\": \"VPN down for the whole office.\", \"priority\": \"urgent\"}", "VPN down for the whole office.", "urgent")]
    [InlineData("```json\n{\"summary\": \"Asks for a refund.\", \"priority\": \"Normal\"}\n```", "Asks for a refund.", "normal")]
    [InlineData("{\"summary\": \"Asks for a refund.\", \"priority\": \"Critical\"}", "Asks for a refund.", null)]
    [InlineData("{\"priority\": \"Urgent\"}", null, null)]
    [InlineData("I can't help with that.", null, null)]
    public void TheTriage_IsReadFromTheAnswer(string answer, string summary, string priorityId)
    {
        // Act
        var triage = TicketAssistant.ParseTriage(answer, [_normal, _urgent]);

        // Assert
        Assert.Equal(summary, triage?.Summary);
        Assert.Equal(priorityId, triage?.Priority?.ItemId);
    }

    [Fact]
    public void TheTranscript_IsOldestFirst_AndMarksWhoWroteWhat()
    {
        // Arrange
        var ticket = Ticket();
        var newestFirst = new[]
        {
            Entry("Checking with the network team.", TicketsConstants.EntryKinds.Note, TimelineVisibility.Internal, default(PartyRef), "Ana"),
            Entry("Have you restarted it?", TicketsConstants.EntryKinds.Reply, TimelineVisibility.Public, new PartyRef(PartyKinds.User, "ana"), "Ana"),
            Entry("The VPN won't connect.", TicketsConstants.EntryKinds.Reply, TimelineVisibility.Public, default(PartyRef), "Dana"),
        };

        // Act
        var text = Assert.Single(TicketAssistant.Transcript(ticket, newestFirst)).Text;

        // Assert
        var requester = text.IndexOf("[Requester] Dana:", StringComparison.Ordinal);
        var agent = text.IndexOf("[Agent] Ana:", StringComparison.Ordinal);
        var note = text.IndexOf("[Internal note] Ana:", StringComparison.Ordinal);
        Assert.StartsWith("Ticket T-7: VPN down", text);
        Assert.True(requester > 0 && agent > requester && note > agent, text);
    }

    [Fact]
    public async Task AReply_IsSuggested_OnlyOnADeskWithAnAssistant()
    {
        // Arrange
        var kit = new Kit("Sorry about that; we're resetting your VPN account now.");
        var withAssistant = Ticket();
        var withoutAssistant = Ticket(deskId: "sales");

        // Act
        var suggested = await kit.Assistant.SuggestReplyAsync(withAssistant, TestContext.Current.CancellationToken);
        var none = await kit.Assistant.SuggestReplyAsync(withoutAssistant, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(suggested.Succeeded, suggested.Error);
        Assert.Equal("Sorry about that; we're resetting your VPN account now.", suggested.Text);
        Assert.False(none.Succeeded);
        kit.Tickets.Verify(service => service.ReplyAsync(It.IsAny<string>(), It.IsAny<TicketReply>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ATriage_IsAnInternalNote_AndSetsThePriorityOnlyWhenTheDeskSaysSo(bool applyPriority)
    {
        // Arrange
        var kit = new Kit("{\"summary\": \"The whole office lost the VPN.\", \"priority\": \"Urgent\"}", applyPriority);

        // Act
        var triage = await kit.Assistant.TriageAsync(Ticket(), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(triage.Succeeded, triage.Error);
        kit.Tickets.Verify(service => service.ReplyAsync("t7", It.Is<TicketReply>(reply => reply.IsInternal && reply.Body.Contains("The whole office lost the VPN.", StringComparison.Ordinal) && reply.Body.Contains("Urgent", StringComparison.Ordinal)), It.IsAny<CancellationToken>()), Times.Once);
        kit.Tickets.Verify(service => service.UpdateAsync("t7", It.Is<TicketUpdate>(update => update.PriorityId == "urgent"), It.IsAny<TicketActor>(), It.IsAny<CancellationToken>()), applyPriority ? Times.Once() : Times.Never());
    }

    private static ContentItem Ticket(string deskId = "support")
    {
        var ticket = new ContentItem { ContentItemId = "t7", ContentType = TicketsConstants.DefaultContentType, DisplayText = "VPN down" };
        ticket.Apply(new TicketPart { Number = "T-7", DeskId = deskId, PriorityId = "normal", Requester = default(PartyRef), RequesterContact = new ContactInfo { Name = "Dana" } });

        return ticket;
    }

    private static TimelineEntry Entry(string body, string kind, TimelineVisibility visibility, PartyRef author, string authorName)
        => new() { Subject = new RecordRef(TicketsConstants.RecordKind, "t7"), Body = body, Kind = kind, Visibility = visibility, Author = author, AuthorName = authorName };

    private sealed class Kit
    {
        public Kit(string answer, bool applyPriority = false)
        {
            var support = new TicketDesk { ItemId = "support", Name = "Support" };
            support.Put(new TicketDeskAISettings { ProfileId = "profile-1", ApplyPriority = applyPriority });
            var sales = new TicketDesk { ItemId = "sales", Name = "Sales" };

            var desks = new Mock<ICatalog<TicketDesk>>();
            desks.Setup(value => value.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string id, CancellationToken _) => id == "support" ? support : id == "sales" ? sales : null);
            var priorities = new Mock<ICatalog<TicketPriority>>();
            priorities.Setup(value => value.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([_normal, _urgent]);
            var timeline = new Mock<ITimelineService>();
            timeline.Setup(value => value.ListAsync(It.IsAny<TimelineQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TimelinePage([Entry("The VPN won't connect.", TicketsConstants.EntryKinds.Reply, TimelineVisibility.Public, default(PartyRef), "Dana")], 1));

            var profile = new AIProfile { ItemId = "profile-1", Name = "Help desk", Type = AIProfileType.Chat };
            var profiles = new Mock<IAIProfileManager>();
            profiles.Setup(value => value.FindByIdAsync("profile-1", It.IsAny<CancellationToken>())).ReturnsAsync(profile);
            var context = new AICompletionContext();
            var contexts = new Mock<IAICompletionContextBuilder>();
            contexts.Setup(value => value.BuildAsync(profile, It.IsAny<Action<AICompletionContext>>(), It.IsAny<CancellationToken>()))
                .Callback((object _, Action<AICompletionContext> configure, CancellationToken _) => configure?.Invoke(context))
                .ReturnsAsync(context);
            var deployment = new AIDeployment { Name = "chat" };
            var deployments = new Mock<IAIDeploymentManager>();
            deployments.Setup(value => value.ResolveSlotAsync(AIDeploymentSlotNames.Chat, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>())).ReturnsAsync(deployment);
            var completions = new Mock<IAICompletionService>();
            completions.Setup(value => value.CompleteAsync(deployment, It.IsAny<IEnumerable<ChatMessage>>(), context, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, answer)));

            Tickets.Setup(service => service.ReplyAsync(It.IsAny<string>(), It.IsAny<TicketReply>(), It.IsAny<CancellationToken>())).ReturnsAsync(new TicketResult());
            Tickets.Setup(service => service.UpdateAsync(It.IsAny<string>(), It.IsAny<TicketUpdate>(), It.IsAny<TicketActor>(), It.IsAny<CancellationToken>())).ReturnsAsync(new TicketResult());

            var services = new ServiceCollection()
                .AddSingleton(Tickets.Object)
                .AddSingleton(profiles.Object)
                .AddSingleton(contexts.Object)
                .AddSingleton(deployments.Object)
                .AddSingleton(completions.Object)
                .BuildServiceProvider();

            Assistant = new TicketAssistant(desks.Object, priorities.Object, timeline.Object, services, NullLogger<TicketAssistant>.Instance, new PassThroughLocalizer());
        }

        public Mock<ITicketService> Tickets { get; } = new();

        public TicketAssistant Assistant { get; }
    }

    private sealed class PassThroughLocalizer : IStringLocalizer<TicketAssistant>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
