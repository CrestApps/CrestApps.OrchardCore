using System.Net;
using CrestApps.Core;
using CrestApps.Core.AI.Clients;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.AI.Core.Handlers;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Workflows.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Conversations;

public sealed class AIContactMemoryAndWebhookTests
{
    private static readonly DateTime _now = new(2026, 10, 26, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Memory_IsSavedEditedAndClearedPerContact()
    {
        var store = new InMemoryMemoryStore();
        var service = Service(store);

        await service.SetAsync("contact1", "  Prefers texts. Waiting on a refund for order 4471.  ", "session1", TestContext.Current.CancellationToken);
        await service.SetAsync("contact2", "VIP", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("Prefers texts. Waiting on a refund for order 4471.", await service.GetAsync("contact1", TestContext.Current.CancellationToken));
        Assert.Equal("session1", store.Items.Single(memory => memory.ContactId == "contact1").LastSessionId);

        await service.SetAsync("contact1", "Refund sent.", cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("Refund sent.", await service.GetAsync("contact1", TestContext.Current.CancellationToken));
        Assert.Equal(2, store.Items.Count);

        await service.SetAsync("contact1", "   ", cancellationToken: TestContext.Current.CancellationToken);
        Assert.Null(await service.GetAsync("contact1", TestContext.Current.CancellationToken));
        Assert.Equal("VIP", await service.GetAsync("contact2", TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Memory_IsClippedAndAppendedToTheInstructions()
    {
        Assert.Equal(AIContactMemory.MaxLength, AIContactMemoryService.Clip(new string('a', 5000)).Length);
        Assert.Equal("Be brief.", AIContactMemoryService.AppendTo("Be brief.", "  "));

        var message = AIContactMemoryService.AppendTo("Be brief.", "Waiting on a refund.");
        Assert.StartsWith("Be brief.\n\n", message);
        Assert.EndsWith("Waiting on a refund.", message);

        var request = AIContactMemoryService.BuildRequest(null, [new AIConversationTurn(AIConversationTurn.CustomerRole, "Where is my refund?", _now)]);
        Assert.Contains("(none yet)", request);
        Assert.Contains("Customer: Where is my refund?", request);
    }

    [Fact]
    public async Task Memory_IsOnlyRewritten_WhenTheProfileSavesItAndTheContactIsKnown()
    {
        var service = Service(new InMemoryMemoryStore());
        var prompts = new[] { new AIChatSessionPrompt { Role = Microsoft.Extensions.AI.ChatRole.User, Content = "Hi", CreatedUtc = _now } };

        var profile = new AIProfile { ItemId = "p1" };
        var session = new AIChatSession { SessionId = "s1" };
        session.Put(new AIConversationContactMetadata { ContactId = "contact1" });

        // The profile does not save memory: nothing is asked of a model, which this service has none of.
        Assert.Null(await service.RememberSessionAsync(profile, session, prompts, TestContext.Current.CancellationToken));

        profile.AlterSettings<AIProfileContactMemorySettings>(settings => settings.SaveMemory = true);
        Assert.Null(await service.RememberSessionAsync(profile, new AIChatSession { SessionId = "s2" }, prompts, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MemoryHandler_AddsTheNote_WhenTheProfileUsesItAndTheContactIsKnown()
    {
        var store = new InMemoryMemoryStore();
        var service = Service(store);
        await service.SetAsync("contact1", "Waiting on a refund.", cancellationToken: TestContext.Current.CancellationToken);

        var handler = new AIContactMemoryCompletionContextBuilderHandler(service, NullLogger<AIContactMemoryCompletionContextBuilderHandler>.Instance);
        var profile = new AIProfile { ItemId = "p1" };
        var context = new AICompletionContext { SystemMessage = "Be brief." };

        using (AIConversationScope.Begin(new AIConversationContext { Contact = new ContentItem { ContentItemId = "contact1" } }))
        {
            await handler.BuiltAsync(new AICompletionContextBuiltContext(profile, context));
            Assert.Equal("Be brief.", context.SystemMessage);

            profile.AlterSettings<AIProfileContactMemorySettings>(settings => settings.UseMemory = true);
            await handler.BuiltAsync(new AICompletionContextBuiltContext(profile, context));
            Assert.EndsWith("Waiting on a refund.", context.SystemMessage);
        }

        var outside = new AICompletionContext { SystemMessage = "Be brief." };
        await handler.BuiltAsync(new AICompletionContextBuiltContext(profile, outside));
        Assert.Equal("Be brief.", outside.SystemMessage);
    }

    [Fact]
    public void Webhook_SignsLikeTheCommerceWebhooks()
    {
        // HMAC-SHA256("secret", "1700000000.{}"), checked against an independent implementation.
        Assert.Equal(
            "v1=" + Convert.ToHexStringLower(System.Security.Cryptography.HMACSHA256.HashData("secret"u8.ToArray(), "1700000000.{}"u8.ToArray())),
            SendSignedWebhookTask.Sign("secret", 1700000000, "{}"));
        Assert.StartsWith("v1=", SendSignedWebhookTask.Sign("secret", 1, "{}"));
        Assert.NotEqual(SendSignedWebhookTask.Sign("secret", 1, "{}"), SendSignedWebhookTask.Sign("secret", 2, "{}"));
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, true)]
    [InlineData(HttpStatusCode.ServiceUnavailable, true)]
    [InlineData(HttpStatusCode.TooManyRequests, true)]
    [InlineData(HttpStatusCode.RequestTimeout, true)]
    [InlineData(HttpStatusCode.BadRequest, false)]
    [InlineData(HttpStatusCode.Unauthorized, false)]
    public void Webhook_RetriesOnlyWhatMaySucceedLater(HttpStatusCode statusCode, bool expected)
    {
        Assert.Equal(expected, SendSignedWebhookTask.IsWorthRetrying(statusCode));
    }

    private static AIContactMemoryService Service(IAIContactMemoryStore store)
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(_now);

        return new AIContactMemoryService(store, Mock.Of<IAIDeploymentManager>(), Mock.Of<IAIClientFactory>(), clock.Object);
    }

    private sealed class InMemoryMemoryStore : IAIContactMemoryStore
    {
        public List<AIContactMemory> Items { get; } = [];

        public Task<AIContactMemory> FindByContactAsync(string contactId, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.FirstOrDefault(memory => memory.ContactId == contactId));

        public Task SaveAsync(AIContactMemory memory, CancellationToken cancellationToken = default)
        {
            if (!Items.Contains(memory))
            {
                Items.Add(memory);
            }

            return Task.CompletedTask;
        }

        public Task DeleteAsync(AIContactMemory memory, CancellationToken cancellationToken = default)
        {
            Items.Remove(memory);

            return Task.CompletedTask;
        }
    }
}
