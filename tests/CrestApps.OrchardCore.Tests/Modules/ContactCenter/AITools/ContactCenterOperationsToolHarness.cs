using CrestApps.Core;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Voice.Models;
using CrestApps.OrchardCore.Omnichannel.Voice.Services;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Tests.Agent;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.AITools;

/// <summary>
/// Builds the services of a Contact Center operations tool call (voice media, call recordings, shared voicemail and
/// live supervision): the module's own access and scope services are real, and the stores and the services that reach
/// a telephony provider are doubles.
/// </summary>
internal sealed class ContactCenterOperationsToolHarness
{
    public static readonly DateTime Now = new(2026, 11, 18, 15, 0, 0, DateTimeKind.Utc);

    public Mock<IVoiceMediaItemManager> VoiceMedia { get; } = new();

    public Mock<IVoiceMediaProvisioningService> Provisioning { get; } = new();

    public Mock<ICallRecordingStore> Recordings { get; } = new();

    public Mock<IRecordingAccessGovernanceService> Governance { get; } = new();

    public Mock<IRecordingMediaStore> RecordingMedia { get; } = new();

    public Mock<IInteractionManager> Interactions { get; } = new();

    public Mock<ISharedVoicemailService> Voicemails { get; } = new();

    public Mock<ISharedVoicemailAuthorizationService> VoicemailAccess { get; } = new();

    public Mock<IActivityQueueManager> Queues { get; } = new();

    public Mock<ISoftPhoneClientNotifier> SoftPhones { get; } = new();

    public Mock<IContactCenterSupervisorInterventionService> Interventions { get; } = new();

    public Mock<IContactCenterMonitoringService> Monitoring { get; } = new();

    public Mock<IContactCenterPhoneCallSupervisionService> PhoneCalls { get; } = new();

    public Mock<ICallSessionManager> Sessions { get; } = new();

    public Mock<IOmnichannelActivityStore> Activities { get; } = new();

    public Mock<IAIVoiceCallSupervision> AISupervision { get; } = new();

    public Mock<IAIVoiceCallTransfers> AITransfers { get; } = new();

    public Mock<CrestApps.Core.AI.IAIChatSessionPromptStore> Prompts { get; } = new();

    public List<AgentProfile> Agents { get; } = [];

    /// <summary>
    /// Gets the queues the signed-in supervisor oversees, by id; a campaign's queue is <c>campaign:{id}</c>.
    /// </summary>
    public HashSet<string> OverseenQueueIds { get; } = new(StringComparer.OrdinalIgnoreCase);

    public IServiceProvider Build(bool signedIn = true, Func<Permission, object, bool> authorize = null)
    {
        var services = AgentToolTestContext.CreateServices(signedIn, authorize);

        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        var clock = new Mock<IClock>();
        clock.Setup(value => value.UtcNow).Returns(Now);
        services.AddSingleton(clock.Object);

        services.AddSingleton(VoiceMedia.Object);
        services.AddSingleton(Provisioning.Object);

        services.AddSingleton(Recordings.Object);
        services.AddSingleton(Governance.Object);
        services.AddSingleton(RecordingMedia.Object);
        services.AddSingleton(Interactions.Object);
        services.AddScoped<CallRecordingAccessEvaluator>();
        services.AddScoped<CallRecordingReader>();
        services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        services.AddScoped<CallRecordingLinks>();

        services.AddSingleton(Voicemails.Object);
        services.AddSingleton(VoicemailAccess.Object);
        services.AddSingleton(Queues.Object);
        services.AddSingleton(SoftPhones.Object);

        services.AddSingleton(Interventions.Object);
        services.AddSingleton(Monitoring.Object);
        services.AddSingleton(PhoneCalls.Object);
        services.AddSingleton(Sessions.Object);
        services.AddSingleton(Activities.Object);
        services.AddSingleton(AISupervision.Object);
        services.AddSingleton(AITransfers.Object);
        services.AddSingleton(Prompts.Object);
        services.AddSingleton(CreateAgentManager());
        services.AddSingleton(CreateQueueAuthorization());
        services.AddScoped<SupervisorCallAccess>();

        return services.BuildServiceProvider();
    }

    public AgentProfile AddAgent(string id, string userName)
    {
        var agent = new AgentProfile
        {
            ItemId = id,
            UserId = $"user-{userName}",
            UserName = userName,
            DisplayName = userName,
        };

        Agents.Add(agent);

        return agent;
    }

    public Interaction AddInteraction(string id, string queueId, string agentId = null)
    {
        var interaction = new Interaction
        {
            ItemId = id,
            QueueId = queueId,
            AgentId = agentId,
        };

        Interactions.Setup(manager => manager.FindByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(interaction);

        return interaction;
    }

    public OmnichannelActivity AddLiveAICall(string id, string campaignId)
    {
        var activity = new OmnichannelActivity
        {
            ItemId = id,
            InteractionType = ActivityInteractionType.Automated,
            Status = ActivityStatus.InProgress,
            Channel = OmnichannelConstants.Channels.Phone,
            CampaignId = campaignId,
            PreferredDestination = "+15550102030",
            AISessionId = $"session-{id}",
        };

        activity.Put(new AILiveCall
        {
            ProviderName = "Test",
            ProviderCallId = $"call-{id}",
            AnsweredUtc = Now.AddMinutes(-3),
        });

        Activities.Setup(store => store.FindByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(activity);

        return activity;
    }

    private IAgentProfileManager CreateAgentManager()
    {
        var manager = new Mock<IAgentProfileManager>();
        manager.Setup(value => value.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => Agents.ToList());
        manager.Setup(value => value.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string id, CancellationToken _) => ValueTask.FromResult(Agents.FirstOrDefault(agent => agent.ItemId == id)));
        manager.Setup(value => value.FindByUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string userId, CancellationToken _) => Agents.FirstOrDefault(agent => agent.UserId == userId));

        return manager.Object;
    }

    private ISupervisorQueueAuthorizationService CreateQueueAuthorization()
    {
        var authorization = new Mock<ISupervisorQueueAuthorizationService>();
        authorization.Setup(value => value.IsAuthorizedAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((System.Security.Claims.ClaimsPrincipal _, string _, string queueId, CancellationToken _) => queueId is not null && OverseenQueueIds.Contains(queueId));

        return authorization.Object;
    }
}
