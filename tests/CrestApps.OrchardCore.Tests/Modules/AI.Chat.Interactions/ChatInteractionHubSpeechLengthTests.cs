using System.Globalization;
using CrestApps.Core.AI.Chat.Hubs;
using CrestApps.OrchardCore.AI.Chat.Interactions.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Chat.Interactions;

/// <summary>
/// G10 of the fourth fix-verification review: the Chat Interactions hub's read-aloud had no length cap, so staff with
/// <c>EditChatInteractions</c> could send several megabytes to the site's text-to-speech provider in one call. Since
/// CrestApps.Core preview.381 the base hub refuses text over <c>MaxSpeechTextLength</c> (10,000 characters); this host's
/// hub does not replace <c>SynthesizeSpeech</c>, so the cap applies, and the refusal is shown in the site's language.
/// </summary>
public sealed class ChatInteractionHubSpeechLengthTests
{
    private const string TooLongMessage = "This message is too long to read aloud.";

    [Fact]
    public void Hub_DoesNotReplaceTheBaseReadAloud()
    {
        var method = typeof(ChatInteractionHub).GetMethod(nameof(ChatInteractionHubBase.SynthesizeSpeech));

        Assert.Equal(typeof(ChatInteractionHubBase), method.DeclaringType);
    }

    [Fact]
    public async Task SynthesizeSpeech_TextOverTenThousandCharacters_IsRefusedWithALocalizedMessage()
    {
        var harness = new Harness();

        await harness.Hub.SynthesizeSpeech("interaction-1", new string('a', 10_001));

        harness.Caller.Verify(client => client.ReceiveError(TooLongMessage), Times.Once);
        harness.Caller.Verify(client => client.ReceiveError(It.Is<string>(message => message != TooLongMessage)), Times.Never);
    }

    [Fact]
    public async Task SynthesizeSpeech_TextOfTenThousandCharacters_PassesTheLengthCheck()
    {
        var harness = new Harness();

        try
        {
            await harness.Hub.SynthesizeSpeech("interaction-1", new string('a', 10_000));
        }
        catch (Exception)
        {
            // The test has no shell to run the synthesis in; only the length check matters here.
        }

        harness.Caller.Verify(client => client.ReceiveError(TooLongMessage), Times.Never);
    }

    private sealed class Harness
    {
        public Harness()
        {
            var clients = new Mock<IHubCallerClients<IChatInteractionHubClient>>();
            clients.SetupGet(c => c.Caller).Returns(Caller.Object);

            var context = new Mock<HubCallerContext>();
            context.SetupGet(c => c.ConnectionAborted).Returns(CancellationToken.None);
            context.SetupGet(c => c.ConnectionId).Returns("connection-1");
            context.SetupGet(c => c.Items).Returns(new Dictionary<object, object>());

            Hub = new ChatInteractionHub(new ServiceCollection().BuildServiceProvider(), TimeProvider.System, NullLogger<ChatInteractionHub>.Instance, new PassThroughLocalizer())
            {
                Clients = clients.Object,
                Context = context.Object,
            };
        }

        public Mock<IChatInteractionHubClient> Caller { get; } = new();

        public ChatInteractionHub Hub { get; }
    }

    private sealed class PassThroughLocalizer : IStringLocalizer<ChatInteractionHub>
    {
        public LocalizedString this[string name]
            => new(name, name);

        public LocalizedString this[string name, params object[] arguments]
            => new(name, string.Format(CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
            => [];
    }
}
