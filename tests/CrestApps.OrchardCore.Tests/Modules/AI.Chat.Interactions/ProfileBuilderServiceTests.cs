using System.Text.Json.Nodes;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Clients;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using CrestApps.Core.Templates.Services;
using CrestApps.OrchardCore.AI.Chat.Interactions.Models;
using CrestApps.OrchardCore.AI.Chat.Interactions.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Chat.Interactions;

public sealed class ProfileBuilderServiceTests
{
    private static readonly DateTime _now = new(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void OrderConversation_WithSharedTimestamp_PlacesCustomerMessageBeforeReply()
    {
        // Arrange
        var reply = Prompt("a1", ChatRole.Assistant, "Hello!", _now);
        var customer = Prompt("u1", ChatRole.User, "Hi", _now);
        var generated = Prompt("g1", ChatRole.Assistant, "Generated", _now.AddSeconds(-1));
        generated.IsGeneratedPrompt = true;

        // Act
        var ordered = ProfileBuilderService.OrderConversation([reply, generated, customer]);

        // Assert
        Assert.Equal(["u1", "a1"], ordered.Select(prompt => prompt.ItemId));
    }

    [Fact]
    public void FindAssistantPrompt_PrefersIdThenTextThenPosition()
    {
        // Arrange
        var conversation = ProfileBuilderService.OrderConversation(
        [
            Prompt("u1", ChatRole.User, "Hi", _now),
            Prompt("a1", ChatRole.Assistant, "Same answer", _now),
            Prompt("u2", ChatRole.User, "Again", _now.AddSeconds(1)),
            Prompt("a2", ChatRole.Assistant, "Same answer", _now.AddSeconds(1)),
            Prompt("u3", ChatRole.User, "More", _now.AddSeconds(2)),
            Prompt("a3", ChatRole.Assistant, "Different", _now.AddSeconds(2)),
        ]);

        // Act & Assert
        Assert.Equal("a1", ProfileBuilderService.FindAssistantPrompt(conversation, "a1", "Different", 2).ItemId);
        Assert.Equal("a3", ProfileBuilderService.FindAssistantPrompt(conversation, null, "  Different\n", null).ItemId);
        Assert.Equal("a2", ProfileBuilderService.FindAssistantPrompt(conversation, null, "Same answer", 1).ItemId);
        Assert.Equal("a3", ProfileBuilderService.FindAssistantPrompt(conversation, "missing", "streamed text that differs", 2).ItemId);
        Assert.Null(ProfileBuilderService.FindAssistantPrompt(conversation, null, null, 9));
    }

    [Fact]
    public void GroupReplaySituations_GroupsRepeatedRejectionsOfOneMessage()
    {
        // Arrange
        var feedback = new[]
        {
            Feedback("f1", "What's the price?", depth: 2, _now),
            Feedback("f2", "What's   the price?", depth: 2, _now.AddMinutes(1)),
            Feedback("f3", "What's the price?", depth: 2, _now.AddMinutes(2)),
            Feedback("f4", "Can you call me?", depth: 4, _now.AddMinutes(3)),
            Feedback("f5", null, depth: 0, _now.AddMinutes(4)),
        };

        // Act
        var situations = ProfileBuilderService.GroupReplaySituations(feedback);

        // Assert
        Assert.Equal(2, situations.Count);
        Assert.Equal(["f1", "f2", "f3"], situations[0].Select(entry => entry.Id));
        Assert.Equal(["f4"], situations[1].Select(entry => entry.Id));
    }

    [Fact]
    public async Task SubmitFeedbackAsync_WithRegenerate_RewritesSettingsKeepsLedgerAndRemovesTheTurn()
    {
        // Arrange
        var interaction = new ChatInteraction
        {
            ItemId = "interaction",
            ChatDeploymentName = "chat",
            SystemMessage = "You sell cars.",
            Temperature = 0.9f,
        };

        var session = new ProfileBuilderSession
        {
            ItemId = "session",
            ChatInteractionId = interaction.ItemId,
            Revisions =
            [
                new ProfileBuilderRevision
                {
                    Number = 1,
                    Source = ProfileBuilderRevisionSource.Initial,
                    Settings = ProfileBuilderService.ReadSettings(interaction, new ProfileBuilderSession()),
                },
            ],
            Feedback =
            [
                new ProfileBuilderFeedback
                {
                    Id = "earlier",
                    Kind = ProfileBuilderFeedbackKind.FeedbackAndRegenerate,
                    CustomerMessage = "Hi",
                    AssistantReply = "HELLO!!! BUY A CAR NOW!!!",
                    Comment = "Never shout.",
                    WasRegenerated = true,
                    RevisionNumber = 1,
                    CreatedUtc = _now.AddMinutes(-5),
                },
            ],
        };

        var customer = Prompt("u1", ChatRole.User, "How much is the sedan?", _now);
        var reply = Prompt("a1", ChatRole.Assistant, "It is $19,999 today only!", _now);
        var prompts = new List<ChatInteractionPrompt> { customer, reply };

        var promptStore = new Mock<IChatInteractionPromptStore>();
        promptStore.Setup(store => store.GetPromptsAsync(interaction.ItemId)).ReturnsAsync(() => prompts.ToArray());
        promptStore.Setup(store => store.DeleteAsync(It.IsAny<ChatInteractionPrompt>(), It.IsAny<CancellationToken>()))
            .Callback<ChatInteractionPrompt, CancellationToken>((prompt, _) => prompts.Remove(prompt))
            .ReturnsAsync(true);

        var chatClient = new RecordingChatClient("""
            {
              "systemMessage": "You sell cars. Never quote a price; offer to connect the customer with sales.",
              "temperature": 3,
              "summary": "Added a rule against quoting prices."
            }
            """);

        var service = CreateService(interaction, promptStore.Object, chatClient, out var interactionManager, out var sessionStore);

        // Act
        var outcome = await service.SubmitFeedbackAsync(session, interaction, new ProfileBuilderFeedbackRequest
        {
            PromptId = "a1",
            Kind = ProfileBuilderFeedbackKind.FeedbackAndRegenerate,
            Comment = "Do not quote prices.",
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(outcome.Succeeded, outcome.Error);
        Assert.Equal("How much is the sedan?", outcome.RegenerateMessage);

        Assert.Equal("You sell cars. Never quote a price; offer to connect the customer with sales.", interaction.SystemMessage);
        Assert.Equal(2f, interaction.Temperature);

        var feedback = Assert.Single(session.Feedback, entry => entry.Id != "earlier");
        Assert.Equal("It is $19,999 today only!", feedback.AssistantReply);
        Assert.Equal("How much is the sedan?", feedback.CustomerMessage);
        Assert.True(feedback.WasRegenerated);
        Assert.Equal(2, feedback.RevisionNumber);

        var revision = session.Revisions[^1];
        Assert.Equal(2, revision.Number);
        Assert.Equal(ProfileBuilderRevisionSource.Feedback, revision.Source);
        Assert.Equal("Added a rule against quoting prices.", revision.Summary);

        Assert.Empty(prompts);
        interactionManager.Verify(manager => manager.UpdateAsync(interaction, It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()), Times.Once);
        sessionStore.Verify(store => store.UpdateAsync(session, It.IsAny<CancellationToken>()), Times.Once);

        // The coach was shown the earlier rejected reply, so the rewrite cannot forget the fix it led to.
        var coachRequest = chatClient.Requests.Single().Last().Text;
        Assert.Contains("HELLO!!! BUY A CAR NOW!!!", coachRequest);
        Assert.Contains("Never shout.", coachRequest);
        Assert.Contains("REJECTED", coachRequest);
        Assert.Contains("Do not quote prices.", coachRequest);
    }

    [Fact]
    public async Task SubmitFeedbackAsync_RegenerateOnAnEarlierReply_IsRefused()
    {
        // Arrange
        var interaction = new ChatInteraction { ItemId = "interaction", SystemMessage = "Be helpful." };
        var session = new ProfileBuilderSession { ItemId = "session", ChatInteractionId = interaction.ItemId };

        var prompts = new[]
        {
            Prompt("u1", ChatRole.User, "Hi", _now),
            Prompt("a1", ChatRole.Assistant, "Hello", _now),
            Prompt("u2", ChatRole.User, "Bye", _now.AddSeconds(1)),
            Prompt("a2", ChatRole.Assistant, "Goodbye", _now.AddSeconds(1)),
        };

        var promptStore = new Mock<IChatInteractionPromptStore>();
        promptStore.Setup(store => store.GetPromptsAsync(interaction.ItemId)).ReturnsAsync(prompts);

        var chatClient = new RecordingChatClient("{}");
        var service = CreateService(interaction, promptStore.Object, chatClient, out _, out _);

        // Act
        var outcome = await service.SubmitFeedbackAsync(session, interaction, new ProfileBuilderFeedbackRequest
        {
            PromptId = "a1",
            Kind = ProfileBuilderFeedbackKind.FeedbackAndRegenerate,
            Comment = "Warmer.",
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(outcome.Succeeded);
        Assert.Empty(session.Feedback);
        Assert.Empty(chatClient.Requests);
        promptStore.Verify(store => store.DeleteAsync(It.IsAny<ChatInteractionPrompt>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SubmitFeedbackAsync_Approval_RecordsWithoutRewriting()
    {
        // Arrange
        var interaction = new ChatInteraction { ItemId = "interaction", SystemMessage = "Be helpful." };
        var session = new ProfileBuilderSession { ItemId = "session", ChatInteractionId = interaction.ItemId };

        var promptStore = new Mock<IChatInteractionPromptStore>();
        promptStore.Setup(store => store.GetPromptsAsync(interaction.ItemId)).ReturnsAsync(
        [
            Prompt("u1", ChatRole.User, "Hi", _now),
            Prompt("a1", ChatRole.Assistant, "Hello there.", _now),
        ]);

        var chatClient = new RecordingChatClient("{}");
        var service = CreateService(interaction, promptStore.Object, chatClient, out var interactionManager, out _);

        // Act
        var outcome = await service.SubmitFeedbackAsync(session, interaction, new ProfileBuilderFeedbackRequest
        {
            ReplyText = "Hello there.",
            Kind = ProfileBuilderFeedbackKind.Approved,
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(outcome.Succeeded);
        var feedback = Assert.Single(session.Feedback);
        Assert.Equal(ProfileBuilderFeedbackKind.Approved, feedback.Kind);
        Assert.Null(feedback.RevisionNumber);
        Assert.Empty(chatClient.Requests);
        Assert.Equal("Be helpful.", interaction.SystemMessage);
        interactionManager.Verify(manager => manager.UpdateAsync(It.IsAny<ChatInteraction>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void CaptureManualChanges_RecordsAnEditMadeInTheSettingsPanel()
    {
        // Arrange
        var interaction = new ChatInteraction { SystemMessage = "Original." };
        var session = new ProfileBuilderSession();
        var service = CreateService(interaction, Mock.Of<IChatInteractionPromptStore>(), new RecordingChatClient("{}"), out _, out _);

        // Act
        var first = service.CaptureManualChanges(session, interaction);
        var unchanged = service.CaptureManualChanges(session, interaction);
        interaction.SystemMessage = "Edited by hand.";
        var edited = service.CaptureManualChanges(session, interaction);

        // Assert
        Assert.True(first);
        Assert.False(unchanged);
        Assert.True(edited);
        Assert.Equal([ProfileBuilderRevisionSource.Initial, ProfileBuilderRevisionSource.Manual], session.Revisions.Select(revision => revision.Source));
        Assert.Equal("Edited by hand.", session.Revisions[^1].Settings.SystemMessage);
    }

    [Fact]
    public async Task RestoreAsync_AppliesTheEarlierRevisionAsANewOne()
    {
        // Arrange
        var interaction = new ChatInteraction { SystemMessage = "Second." };
        var session = new ProfileBuilderSession
        {
            InitialPrompt = "Open politely.",
            Revisions =
            [
                new ProfileBuilderRevision { Number = 1, Settings = new ProfileBuilderSettings { SystemMessage = "First.", Temperature = 0.2f } },
                new ProfileBuilderRevision { Number = 2, Settings = new ProfileBuilderSettings { SystemMessage = "Second.", InitialPrompt = "Open politely." } },
            ],
        };

        var service = CreateService(interaction, Mock.Of<IChatInteractionPromptStore>(), new RecordingChatClient("{}"), out _, out _);

        // Act
        var revision = await service.RestoreAsync(session, interaction, 1, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(3, revision.Number);
        Assert.Equal(ProfileBuilderRevisionSource.Restore, revision.Source);
        Assert.Equal("First.", interaction.SystemMessage);
        Assert.Equal(0.2f, interaction.Temperature);
        Assert.Null(session.InitialPrompt);
    }

    private static ProfileBuilderService CreateService(
        ChatInteraction interaction,
        IChatInteractionPromptStore promptStore,
        IChatClient chatClient,
        out Mock<ICatalogManager<ChatInteraction>> interactionManager,
        out Mock<IProfileBuilderSessionStore> sessionStore)
    {
        interactionManager = new Mock<ICatalogManager<ChatInteraction>>();
        interactionManager.Setup(manager => manager.FindByIdAsync(interaction.ItemId, It.IsAny<CancellationToken>())).ReturnsAsync(interaction);

        sessionStore = new Mock<IProfileBuilderSessionStore>();

        var deploymentManager = new Mock<IAIDeploymentManager>();
        deploymentManager
            .Setup(manager => manager.ResolveSlotAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIDeployment { Name = "chat" });

        var clientFactory = new Mock<IAIClientFactory>();
        clientFactory
            .Setup(factory => factory.CreateChatClientAsync(It.IsAny<AIDeployment>(), It.IsAny<Action<ChatClientBuilder>>()))
            .ReturnsAsync(chatClient);

        var templateService = new Mock<ITemplateService>();
        templateService
            .Setup(service => service.RenderAsync(It.IsAny<string>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("You are a prompt engineer.");

        var clock = new Mock<IClock>();
        clock.Setup(value => value.UtcNow).Returns(_now);

        var coach = new ProfileBuilderCoach(deploymentManager.Object, clientFactory.Object, templateService.Object);

        return new ProfileBuilderService(
            interactionManager.Object,
            promptStore,
            sessionStore.Object,
            coach,
            deploymentManager.Object,
            Mock.Of<IAICompletionService>(),
            Mock.Of<IAICompletionContextBuilder>(),
            clock.Object,
            NullLogger<ProfileBuilderService>.Instance);
    }

    private static ChatInteractionPrompt Prompt(string id, ChatRole role, string text, DateTime createdUtc)
        => new()
        {
            ItemId = id,
            ChatInteractionId = "interaction",
            Role = role,
            Text = text,
            CreatedUtc = createdUtc,
        };

    private static ProfileBuilderFeedback Feedback(string id, string customerMessage, int depth, DateTime createdUtc)
        => new()
        {
            Id = id,
            CustomerMessage = customerMessage,
            AssistantReply = "reply",
            Context = Enumerable.Range(0, depth).Select(index => new ProfileBuilderTurn { Role = index % 2 == 0 ? "user" : "assistant", Text = "turn" }).ToList(),
            CreatedUtc = createdUtc,
        };

    private sealed class RecordingChatClient : IChatClient
    {
        private readonly string _response;

        public RecordingChatClient(string response)
        {
            _response = response;
        }

        public List<List<ChatMessage>> Requests { get; } = [];

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions options = null, CancellationToken cancellationToken = default)
        {
            Requests.Add(messages.ToList());

            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, _response)));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions options = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public object GetService(Type serviceType, object serviceKey = null)
            => null;

        public void Dispose()
        {
        }
    }
}
