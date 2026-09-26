using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Endpoints;
using CrestApps.OrchardCore.ContactCenter.Handlers;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Core.Services;
using CrestApps.OrchardCore.Telephony.Hubs;
using CrestApps.OrchardCore.Telephony.Indexes;
using CrestApps.OrchardCore.Telephony.Migrations;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telephony.Services;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;
using YesSqlSession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Proves, against a real soft-phone inbox, that the voicemails an agent can see are exactly the ones they can play
/// and delete, and that nobody else's voicemail can be reached.
/// </summary>
public sealed class VoicemailOwnershipEndpointTests : IAsyncLifetime
{
    private const string OwnerUserId = "user-1";
    private const string OtherUserId = "user-2";

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"crestapps-voicemail-ownership-{Guid.NewGuid():N}.db");
    private readonly List<Interaction> _interactions = [];
    private readonly Mock<IRecordingMediaStore> _mediaStore = new();
    private readonly Mock<IRecordingAccessGovernanceService> _governance = new();
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_databasePath};Pooling=False"));
        _store.RegisterIndexes([new TelephonyInteractionIndexProvider()]);
        await _store.InitializeAsync(TestContext.Current.CancellationToken);

        await using var session = _store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var migration = new TelephonyInteractionMigrations
        {
            SchemaBuilder = new SchemaBuilder(_store.Configuration, transaction),
        };

        await migration.CreateAsync();
        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        _mediaStore
            .Setup(store => store.OpenReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream([1, 2, 3]));
        _mediaStore
            .Setup(store => store.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _governance
            .Setup(governance => governance.RecordAccessAsync(It.IsAny<string>(), It.IsAny<ContactCenterActor>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _databasePath);

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Delete_WhenAQueueVoicemailSitsInTheAgentsInbox_DeletesIt()
    {
        // Arrange
        // The incident: an AI-to-agent handoff offered the caller to the agent, the offer expired, and the caller
        // reached the queue's voicemail on max wait. The interaction carries no agent and no voicemail recipient, yet
        // the soft phone listed the voicemail in the agent's inbox (under a row id of its own). Delete then answered
        // 403-by-redirect, the soft phone reported nothing, and the voicemail stayed.
        _interactions.Add(QueueVoicemail("cc-1", "call-1", "rec-1"));
        await SeedRowAsync("row-1", "call-1", OwnerUserId, isVoicemail: true);

        // Act
        var (status, location) = await DeleteAsync("row-1", OwnerUserId);

        // Assert
        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Null(location);
        _mediaStore.Verify(store => store.DeleteAsync("rec-1", It.IsAny<CancellationToken>()), Times.Once);
        _governance.Verify(governance => governance.EraseAsync("cc-1", ContactCenterActor.Agent(OwnerUserId), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Null(await ReadRowAsync(OwnerUserId, "call-1"));
    }

    [Fact]
    public async Task Play_WhenAQueueVoicemailSitsInTheAgentsInbox_StreamsIt()
    {
        // Arrange
        _interactions.Add(QueueVoicemail("cc-1", "call-1", "rec-1"));
        await SeedRowAsync("row-1", "call-1", OwnerUserId, isVoicemail: true);

        // Act
        var (status, _) = await PlayAsync("row-1", OwnerUserId);

        // Assert
        Assert.Equal(StatusCodes.Status200OK, status);
        _governance.Verify(governance => governance.RecordAccessAsync("cc-1", ContactCenterActor.Agent(OwnerUserId), "voicemail-playback", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProjectedQueueVoicemail_TheVoicemailTheListShows_CanBePlayedAndDeleted()
    {
        // Arrange
        // The soft phone's list is the agent's own inbox rows flagged as voicemail. Project a queue voicemail through
        // the real projection handler, read the list the way the soft phone does, and act on what it shows.
        var interaction = QueueVoicemail("cc-1", "call-1", "rec-1");
        _interactions.Add(interaction);
        await ProjectAsync(interaction, sessionAgentId: "agent-1");

        IReadOnlyList<TelephonyInteraction> listed;

        await using (var session = _store.CreateSession())
        {
            listed = [.. (await CreateInteractionStore(session).GetRecentAsync(OwnerUserId, 30, TestContext.Current.CancellationToken)).Where(row => row.IsVoicemail)];
        }

        var shown = Assert.Single(listed);

        // Act
        var (playStatus, _) = await PlayAsync(shown.InteractionId, OwnerUserId);
        var (deleteStatus, _) = await DeleteAsync(shown.InteractionId, OwnerUserId);

        // Assert
        Assert.Equal(StatusCodes.Status200OK, playStatus);
        Assert.Equal(StatusCodes.Status200OK, deleteStatus);
        Assert.Null(await ReadRowAsync(OwnerUserId, "call-1"));
    }

    [Fact]
    public async Task Delete_WhenTheVoicemailIsAnotherAgentsByInteractionId_IsForbiddenWithoutARedirect()
    {
        // Arrange
        _interactions.Add(QueueVoicemail("cc-1", "call-1", "rec-1"));
        await SeedRowAsync("row-1", "call-1", OwnerUserId, isVoicemail: true);

        // Act
        var (status, location) = await DeleteAsync("cc-1", OtherUserId);

        // Assert
        Assert.Equal(StatusCodes.Status403Forbidden, status);
        Assert.Null(location);
        _mediaStore.Verify(store => store.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _governance.Verify(governance => governance.EraseAsync(It.IsAny<string>(), It.IsAny<ContactCenterActor>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.NotNull(await ReadRowAsync(OwnerUserId, "call-1"));
    }

    [Fact]
    public async Task Delete_WhenTheIdIsAnotherAgentsInboxRow_IsNotFound()
    {
        // Arrange
        _interactions.Add(QueueVoicemail("cc-1", "call-1", "rec-1"));
        await SeedRowAsync("row-1", "call-1", OwnerUserId, isVoicemail: true);

        // Act
        var (status, location) = await DeleteAsync("row-1", OtherUserId);

        // Assert
        Assert.Equal(StatusCodes.Status404NotFound, status);
        Assert.Null(location);
        Assert.NotNull(await ReadRowAsync(OwnerUserId, "call-1"));
    }

    [Fact]
    public async Task PlayAndDelete_WhenTheCallerOnlyHandledTheCallBeforeItReachedAnotherAgentsVoicemail_AreForbidden()
    {
        // Arrange
        // The second agent talked to the caller and transferred them; the call then went to the first agent's
        // voicemail. The second agent's inbox holds the call, but not as a voicemail, so the recording is not theirs.
        _interactions.Add(DirectVoicemail("cc-2", "call-2", "rec-2", recipientAgentId: "agent-1"));
        await SeedRowAsync("row-2", "call-2", OtherUserId, isVoicemail: false);
        await SeedRowAsync("cc-2", "call-2", OwnerUserId, isVoicemail: true);

        // Act
        var (playStatus, playLocation) = await PlayAsync("cc-2", OtherUserId);
        var (deleteStatus, deleteLocation) = await DeleteAsync("row-2", OtherUserId);

        // Assert
        Assert.Equal(StatusCodes.Status403Forbidden, playStatus);
        Assert.Null(playLocation);
        Assert.Equal(StatusCodes.Status403Forbidden, deleteStatus);
        Assert.Null(deleteLocation);
        _governance.Verify(governance => governance.RecordAccessAsync(It.IsAny<string>(), It.IsAny<ContactCenterActor>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _mediaStore.Verify(store => store.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.NotNull(await ReadRowAsync(OtherUserId, "call-2"));
        Assert.NotNull(await ReadRowAsync(OwnerUserId, "call-2"));
    }

    [Fact]
    public async Task Delete_WhenADirectVoicemailIsAddressedByItsInteractionId_DeletesTheRecipientsRow()
    {
        // Arrange
        _interactions.Add(DirectVoicemail("cc-2", "call-2", "rec-2", recipientAgentId: "agent-1"));
        await SeedRowAsync("cc-2", "call-2", OwnerUserId, isVoicemail: true);

        // Act
        var (status, _) = await DeleteAsync("cc-2", OwnerUserId);

        // Assert
        Assert.Equal(StatusCodes.Status200OK, status);
        _mediaStore.Verify(store => store.DeleteAsync("rec-2", It.IsAny<CancellationToken>()), Times.Once);
        Assert.Null(await ReadRowAsync(OwnerUserId, "call-2"));
    }

    [Fact]
    public async Task Delete_WhenTheInboxVoicemailOutlivedItsInteraction_StillRemovesItFromTheInbox()
    {
        // Arrange
        // The agent can see it, so the agent can delete it -- even when the platform interaction behind it is gone.
        await SeedRowAsync("row-3", "call-3", OwnerUserId, isVoicemail: true);

        // Act
        var (status, _) = await DeleteAsync("row-3", OwnerUserId);

        // Assert
        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Null(await ReadRowAsync(OwnerUserId, "call-3"));
    }

    [Fact]
    public async Task PlayAndDelete_WhenSignedOut_AnswerUnauthorizedWithoutARedirect()
    {
        // Arrange
        _interactions.Add(QueueVoicemail("cc-1", "call-1", "rec-1"));
        await SeedRowAsync("row-1", "call-1", OwnerUserId, isVoicemail: true);

        // Act
        var (playStatus, playLocation) = await PlayAsync("row-1", userId: null);
        var (deleteStatus, deleteLocation) = await DeleteAsync("row-1", userId: null);

        // Assert
        Assert.Equal(StatusCodes.Status401Unauthorized, playStatus);
        Assert.Null(playLocation);
        Assert.Equal(StatusCodes.Status401Unauthorized, deleteStatus);
        Assert.Null(deleteLocation);
        Assert.NotNull(await ReadRowAsync(OwnerUserId, "call-1"));
    }

    [Fact]
    public async Task PlayAndDelete_AreAuditedAsTheAgentsOwnActs()
    {
        // Arrange
        // The recording audit trail is read to answer "who listened to this, who erased it"; an actor recorded as
        // unspecified answers neither.
        _interactions.Add(QueueVoicemail("cc-1", "call-1", "rec-1"));
        await SeedRowAsync("row-1", "call-1", OwnerUserId, isVoicemail: true);
        var (governance, published) = CreateRealGovernance();

        // Act
        await PlayAsync("row-1", OwnerUserId, governance);
        var (status, _) = await DeleteAsync("row-1", OwnerUserId, governance);

        // Assert
        Assert.Equal(StatusCodes.Status200OK, status);
        var accessed = Assert.Single(published, e => e.EventType == ContactCenterConstants.Events.RecordingAccessed);
        var erased = Assert.Single(published, e => e.EventType == ContactCenterConstants.Events.RecordingErased);
        Assert.Equal(ContactCenterActorType.Agent, accessed.ActorType);
        Assert.Equal(OwnerUserId, accessed.ActorId);
        Assert.Equal(ContactCenterActorType.Agent, erased.ActorType);
        Assert.Equal(OwnerUserId, erased.ActorId);
    }

    [Fact]
    public async Task Delete_WhenTheVoicemailRecordedNothing_RemovesItWithoutADeniedErasure()
    {
        // Arrange
        // The second stuck voicemail: a direct call the agent sent to voicemail with the Voicemail button, where the
        // caller hung up during the greeting. Nothing was recorded, so there is nothing to erase -- asking governance
        // to erase it anyway wrote a RecordingErasureDenied audit entry for a delete that did what the agent asked.
        var interaction = DirectVoicemail("cc-4", "call-4", storageReference: null, recipientAgentId: "agent-1");
        _interactions.Add(interaction);
        await SeedRowAsync("row-4", "call-4", OwnerUserId, isVoicemail: true);
        var (governance, published) = CreateRealGovernance();

        // Act
        var (status, _) = await DeleteAsync("row-4", OwnerUserId, governance);

        // Assert
        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Null(await ReadRowAsync(OwnerUserId, "call-4"));
        Assert.DoesNotContain(published, e => e.EventType == ContactCenterConstants.Events.RecordingErasureDenied);
    }

    [Fact]
    public async Task Delete_WhenTheRecordingIsUnderLegalHold_KeepsTheMediaAndTheVoicemail()
    {
        // Arrange
        // A legal hold outranks the agent's delete. The media was deleted before governance was asked, so a held
        // recording was destroyed and only the audit said it had been refused.
        var interaction = DirectVoicemail("cc-5", "call-5", "rec-5", recipientAgentId: "agent-1");
        interaction.RecordingLegalHold = true;
        _interactions.Add(interaction);
        await SeedRowAsync("row-5", "call-5", OwnerUserId, isVoicemail: true);
        var (governance, _) = CreateRealGovernance();

        // Act
        var (status, location) = await DeleteAsync("row-5", OwnerUserId, governance);

        // Assert
        Assert.Equal(StatusCodes.Status409Conflict, status);
        Assert.Null(location);
        _mediaStore.Verify(store => store.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.NotNull(await ReadRowAsync(OwnerUserId, "call-5"));
    }

    private (IRecordingAccessGovernanceService Governance, List<InteractionEvent> Published) CreateRealGovernance()
    {
        var published = new List<InteractionEvent>();
        var publisher = new Mock<IContactCenterEventPublisher>();
        publisher
            .Setup(value => value.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()))
            .Callback<InteractionEvent, CancellationToken>((interactionEvent, _) => published.Add(interactionEvent))
            .Returns(Task.CompletedTask);

        var governance = new RecordingAccessGovernanceService(
            CreateInteractionManager().Object,
            new Mock<ICallSessionManager>().Object,
            publisher.Object,
            new Mock<IClock>().Object);

        return (governance, published);
    }

    private static Interaction QueueVoicemail(string itemId, string callId, string storageReference)
    {
        var interaction = new Interaction
        {
            ItemId = itemId,
            ProviderName = "Telnyx",
            ProviderInteractionId = callId,
            QueueId = "queue-support",
            AgentId = null,
            Direction = InteractionDirection.Inbound,
            CustomerAddress = "+15550001000",
            CreatedUtc = new DateTime(2026, 9, 24, 22, 44, 34, DateTimeKind.Utc),
            StartedUtc = new DateTime(2026, 9, 24, 22, 44, 34, DateTimeKind.Utc),
            RecordingReference = storageReference,
            TechnicalMetadata = new Dictionary<string, object>
            {
                ["routing_terminal_reason"] = "queue_max_wait_voicemail",
                [ContactCenterConstants.Voicemail.ProjectionMetadataKey] = true,
            },
        };

        if (storageReference is not null)
        {
            interaction.TechnicalMetadata[ContactCenterConstants.RecordingMetadata.StorageReference] = storageReference;
        }

        return interaction.RestorePersistedStatus(InteractionStatus.Ended);
    }

    private static Interaction DirectVoicemail(string itemId, string callId, string storageReference, string recipientAgentId)
    {
        var interaction = QueueVoicemail(itemId, callId, storageReference);
        interaction.QueueId = ContactCenterConstants.DirectRouting.QueueId;
        interaction.TechnicalMetadata[ContactCenterConstants.Voicemail.RecipientAgentMetadataKey] = recipientAgentId;

        return interaction;
    }

    private async Task SeedRowAsync(string interactionId, string callId, string userId, bool isVoicemail)
    {
        await using var session = _store.CreateSession();
        await CreateInteractionStore(session).CreateAsync(new TelephonyInteraction
        {
            InteractionId = interactionId,
            CallId = callId,
            ProviderName = "Telnyx",
            UserId = userId,
            UserName = userId,
            Direction = CallDirection.Inbound,
            Outcome = CallOutcome.Missed,
            StartedUtc = new DateTime(2026, 9, 24, 22, 44, 34, DateTimeKind.Utc),
            EndedUtc = new DateTime(2026, 9, 24, 22, 45, 41, DateTimeKind.Utc),
            IsVoicemail = isVoicemail,
        }, TestContext.Current.CancellationToken);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<TelephonyInteraction> ReadRowAsync(string userId, string callId)
    {
        await using var session = _store.CreateSession();

        return await CreateInteractionStore(session).FindByCallIdAsync(userId, callId, TestContext.Current.CancellationToken);
    }

    private DefaultTelephonyInteractionStore CreateInteractionStore(YesSqlSession session)
    {
        return new DefaultTelephonyInteractionStore(session, _store, new ProviderIdentityResolver([]), []);
    }

    private async Task ProjectAsync(Interaction interaction, string sessionAgentId)
    {
        var callSessionManager = new Mock<ICallSessionManager>();
        callSessionManager
            .Setup(manager => manager.FindByInteractionIdAsync(interaction.ItemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CallSession
            {
                ItemId = "session-1",
                InteractionId = interaction.ItemId,
                AgentId = sessionAgentId,
                ProviderName = "Telnyx",
                ProviderCallId = interaction.ProviderInteractionId,
                StartedUtc = interaction.StartedUtc ?? interaction.CreatedUtc,
            });

        var hubContext = new Mock<IHubContext<TelephonyHub, ITelephonyClient>>();
        var clients = new Mock<IHubClients<ITelephonyClient>>();
        clients
            .Setup(value => value.Group(It.IsAny<string>()))
            .Returns(new Mock<ITelephonyClient>().Object);
        hubContext
            .SetupGet(value => value.Clients)
            .Returns(clients.Object);

        await using var session = _store.CreateSession();
        var handler = new ContactCenterSoftPhoneEventHandler(
            CreateInteractionManager().Object,
            callSessionManager.Object,
            CreateAgentProfileManager().Object,
            CreateInteractionStore(session),
            hubContext.Object,
            new ShellSettings { Name = "Default" });

        await handler.HandleAsync(new InteractionEvent
        {
            EventType = ContactCenterConstants.Events.CallSentToVoicemail,
            InteractionId = interaction.ItemId,
        }, TestContext.Current.CancellationToken);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private Mock<IInteractionManager> CreateInteractionManager()
    {
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(manager => manager.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, CancellationToken _) => _interactions.FirstOrDefault(interaction => interaction.ItemId == id));
        interactionManager
            .Setup(manager => manager.FindByProviderInteractionIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string callId, CancellationToken _) => _interactions.FirstOrDefault(interaction => interaction.ProviderInteractionId == callId));

        return interactionManager;
    }

    private static Mock<IAgentProfileManager> CreateAgentProfileManager()
    {
        var agentProfileManager = new Mock<IAgentProfileManager>();
        agentProfileManager
            .Setup(manager => manager.FindByIdAsync("agent-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile { ItemId = "agent-1", UserId = OwnerUserId, UserName = OwnerUserId });
        agentProfileManager
            .Setup(manager => manager.FindByIdAsync("agent-2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile { ItemId = "agent-2", UserId = OtherUserId, UserName = OtherUserId });

        return agentProfileManager;
    }

    private async Task<(int Status, string Location)> PlayAsync(string interactionId, string userId, IRecordingAccessGovernanceService governance = null)
    {
        await using var session = _store.CreateSession();
        var httpContext = CreateHttpContext(userId, governance);

        var result = await AgentWorkspaceEndpoints.HandleVoicemailMediaAsync(
            interactionId,
            CreateInteractionManager().Object,
            CreateInteractionStore(session),
            httpContext);

        return await ExecuteAsync(result, httpContext);
    }

    private async Task<(int Status, string Location)> DeleteAsync(string interactionId, string userId, IRecordingAccessGovernanceService governance = null)
    {
        await using var session = _store.CreateSession();
        var httpContext = CreateHttpContext(userId, governance);

        var result = await AgentWorkspaceEndpoints.HandleDeleteVoicemailAsync(
            interactionId,
            CreateInteractionManager().Object,
            CreateInteractionStore(session),
            new Mock<IAntiforgery>().Object,
            httpContext);

        await session.SaveChangesAsync(TestContext.Current.CancellationToken);

        return await ExecuteAsync(result, httpContext);
    }

    /// <summary>
    /// Runs the endpoint's result the way the host does, through the site's cookie authentication, so a result that
    /// would turn into a sign-in or access-denied redirect shows up as one.
    /// </summary>
    private static async Task<(int Status, string Location)> ExecuteAsync(IResult result, DefaultHttpContext httpContext)
    {
        await result.ExecuteAsync(httpContext);

        var location = httpContext.Response.Headers.Location.ToString();

        return (httpContext.Response.StatusCode, string.IsNullOrEmpty(location) ? null : location);
    }

    private DefaultHttpContext CreateHttpContext(string userId, IRecordingAccessGovernanceService governance = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(_mediaStore.Object);
        services.AddSingleton(governance ?? _governance.Object);
        services
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie();

        var identity = string.IsNullOrEmpty(userId)
            ? new ClaimsIdentity()
            : new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], "Test");

        return new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity),
            RequestServices = services.BuildServiceProvider(),
            Response =
            {
                Body = new MemoryStream(),
            },
        };
    }
}
