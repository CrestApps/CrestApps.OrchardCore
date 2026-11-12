using System.Security.Claims;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Endpoints;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Voice.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;
using OrchardCore.Security;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// SEC-P7: supervision of people's calls is limited to the supervisor's queues and campaigns, but the AI-call list,
/// transcript, note, take-over, end and listen handlers checked only the tenant-wide permissions, and the transcript
/// answered for any activity with an AI session, live or long over. A supervisor now sees and acts on the AI calls of
/// their campaigns (every AI call with Monitor every AI call), and a past call's transcript needs Listen to anyone's call
/// recordings.
/// </summary>
public sealed class SupervisorAICallScopeTests
{
    private const string SupervisorId = "supervisor-1";

    [Fact]
    public async Task AScopedSupervisor_CanReadTheTranscriptOfTheirCampaignsAICall_ButNotAnothers()
    {
        // Arrange
        var harness = new Harness(MonitorAndIntervene());

        // Act
        var ours = await SupervisorAICallsEndpoints.HandleTranscriptAsync("ours", null, harness.Authorization, harness.Activities.Object, harness.Prompts.Object, harness.QueueAuthorization, Harness.Request());
        var theirs = await SupervisorAICallsEndpoints.HandleTranscriptAsync("theirs", null, harness.Authorization, harness.Activities.Object, harness.Prompts.Object, harness.QueueAuthorization, Harness.Request());
        var unscoped = await SupervisorAICallsEndpoints.HandleTranscriptAsync("unscoped", null, harness.Authorization, harness.Activities.Object, harness.Prompts.Object, harness.QueueAuthorization, Harness.Request());

        // Assert
        Assert.Equal(200, Status(ours));
        Assert.Equal(403, Status(theirs));
        Assert.Equal(403, Status(unscoped));
    }

    [Fact]
    public async Task TheTranscriptOfAnEndedCall_NeedsTheRecordingsPermission()
    {
        // Arrange
        var withoutRecordings = new Harness(MonitorAndIntervene());
        var withRecordings = new Harness([.. MonitorAndIntervene(), ContactCenterPermissions.ListenToAllCallRecordings.Name]);

        // Act
        var refused = await SupervisorAICallsEndpoints.HandleTranscriptAsync("ended", null, withoutRecordings.Authorization, withoutRecordings.Activities.Object, withoutRecordings.Prompts.Object, withoutRecordings.QueueAuthorization, Harness.Request());
        var allowed = await SupervisorAICallsEndpoints.HandleTranscriptAsync("ended", null, withRecordings.Authorization, withRecordings.Activities.Object, withRecordings.Prompts.Object, withRecordings.QueueAuthorization, Harness.Request());

        // Assert
        Assert.Equal(403, Status(refused));
        Assert.Equal(200, Status(allowed));
    }

    [Fact]
    public async Task MonitorEveryAICall_OpensTheCallsOfAnyCampaign()
    {
        var harness = new Harness([.. MonitorAndIntervene(), ContactCenterPermissions.MonitorAllAICalls.Name]);

        var theirs = await SupervisorAICallsEndpoints.HandleTranscriptAsync("theirs", null, harness.Authorization, harness.Activities.Object, harness.Prompts.Object, harness.QueueAuthorization, Harness.Request());
        var unscoped = await SupervisorAICallsEndpoints.HandleTranscriptAsync("unscoped", null, harness.Authorization, harness.Activities.Object, harness.Prompts.Object, harness.QueueAuthorization, Harness.Request());

        Assert.Equal(200, Status(theirs));
        Assert.Equal(200, Status(unscoped));
    }

    [Fact]
    public async Task AScopedSupervisor_CantSendANote_TakeOver_EndOrListenTo_AnotherCampaignsAICall()
    {
        // Arrange
        var harness = new Harness(MonitorAndIntervene());

        // Act
        var note = await SupervisorAICallsEndpoints.HandleNoteAsync("theirs", new SupervisorAICallsEndpoints.NoteRequest { Note = "Offer the discount." }, harness.Authorization, harness.Antiforgery.Object, [harness.Supervision.Object], harness.Activities.Object, harness.QueueAuthorization, Harness.Request());
        var takeOver = await SupervisorAICallsEndpoints.HandleTakeOverAsync("theirs", harness.Authorization, harness.Antiforgery.Object, [harness.Supervision.Object], harness.Agents.Object, harness.Activities.Object, harness.QueueAuthorization, Harness.Request());
        var end = await SupervisorAICallsEndpoints.HandleEndAsync("theirs", harness.Authorization, harness.Antiforgery.Object, [harness.Supervision.Object], harness.Activities.Object, harness.QueueAuthorization, Harness.Request());
        var listen = await SupervisorAICallsEndpoints.HandleListenAsync("theirs", harness.Authorization, harness.Antiforgery.Object, harness.Activities.Object, harness.PhoneCalls.Object, harness.QueueAuthorization, Harness.Request());

        // Assert
        Assert.Equal(403, Status(note));
        Assert.Equal(403, Status(takeOver));
        Assert.Equal(403, Status(end));
        Assert.Equal(403, Status(listen));
        harness.Supervision.VerifyNoOtherCalls();
        harness.PhoneCalls.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task TheList_HoldsOnlyTheSupervisorsAICalls()
    {
        var harness = new Harness(MonitorAndIntervene());

        var visible = await SupervisorAICallScope.FilterAsync(
            [harness.Activity("ours"), harness.Activity("theirs"), harness.Activity("unscoped")],
            harness.Authorization,
            harness.QueueAuthorization,
            Harness.Request().User,
            TestContext.Current.CancellationToken);

        Assert.Equal(["ours"], visible.Select(activity => activity.ItemId));
    }

    private static string[] MonitorAndIntervene()
        => [ContactCenterPermissions.MonitorContactCenter.Name, ContactCenterPermissions.InterveneInCalls.Name];

    private static int Status(IResult result)
        => result switch
        {
            IStatusCodeHttpResult { StatusCode: { } code } => code,
            ForbidHttpResult => 403,
            _ => 0,
        };

    private sealed class Harness
    {
        private readonly Dictionary<string, OmnichannelActivity> _activities = new(StringComparer.Ordinal);

        public Harness(IEnumerable<string> permissions)
        {
            Authorization = new GrantingAuthorizationService(permissions);

            Agents.Setup(manager => manager.FindByUserIdAsync(SupervisorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AgentProfile { ItemId = "agent-1", UserId = SupervisorId, AllowedCampaignIds = ["campaign-a"] });
            QueueAuthorization = new SupervisorQueueAuthorizationService(Authorization, Agents.Object);

            Add("ours", "campaign-a", live: true);
            Add("theirs", "campaign-b", live: true);
            Add("unscoped", null, live: true);
            Add("ended", "campaign-a", live: false);

            Activities.Setup(store => store.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string id, CancellationToken _) => _activities.GetValueOrDefault(id));
            Prompts.Setup(store => store.GetPromptsAsync(It.IsAny<string>())).ReturnsAsync(Array.Empty<AIChatSessionPrompt>());
            Antiforgery.Setup(antiforgery => antiforgery.ValidateRequestAsync(It.IsAny<HttpContext>())).Returns(Task.CompletedTask);
        }

        public IAuthorizationService Authorization { get; }

        public ISupervisorQueueAuthorizationService QueueAuthorization { get; }

        public Mock<IAgentProfileManager> Agents { get; } = new();

        public Mock<IOmnichannelActivityStore> Activities { get; } = new();

        public Mock<IAIChatSessionPromptStore> Prompts { get; } = new();

        public Mock<IAntiforgery> Antiforgery { get; } = new();

        public Mock<IAIVoiceCallSupervision> Supervision { get; } = new(MockBehavior.Strict);

        public Mock<IContactCenterPhoneCallSupervisionService> PhoneCalls { get; } = new(MockBehavior.Strict);

        public OmnichannelActivity Activity(string id) => _activities[id];

        public static DefaultHttpContext Request()
            => new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, SupervisorId), new Claim(ClaimTypes.Name, "supervisor")], "test")),
            };

        private void Add(string id, string campaignId, bool live)
            => _activities[id] = new OmnichannelActivity
            {
                ItemId = id,
                CampaignId = campaignId,
                AISessionId = "session-" + id,
                Channel = OmnichannelConstants.Channels.Phone,
                InteractionType = ActivityInteractionType.Automated,
                Status = live ? ActivityStatus.InProgress : ActivityStatus.Completed,
            };
    }

    private sealed class GrantingAuthorizationService : IAuthorizationService
    {
        private readonly HashSet<string> _granted;

        public GrantingAuthorizationService(IEnumerable<string> granted)
        {
            _granted = new HashSet<string>(granted, StringComparer.Ordinal);
        }

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements)
            => Task.FromResult(requirements.OfType<PermissionRequirement>().All(requirement => _granted.Contains(requirement.Permission.Name))
                ? AuthorizationResult.Success()
                : AuthorizationResult.Failed());

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, string policyName)
            => Task.FromResult(AuthorizationResult.Failed());
    }
}
