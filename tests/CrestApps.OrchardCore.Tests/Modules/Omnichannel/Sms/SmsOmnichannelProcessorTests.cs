using CrestApps.Core.Omnichannel.Models;
using CrestApps.Core.Omnichannel.Services;
using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Sms.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Fluid.Values;
using Microsoft.AspNetCore.Html;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Time.Testing;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Infrastructure;
using OrchardCore.Liquid;
using OrchardCore.Sms;
using System.Text.Encodings.Web;
using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Sms;

/// <summary>
/// Guards the last piece of code between a loaded activity and a real text message arriving on somebody's phone.
/// The failures it prevents are the ones a customer sees: an opening message sent from a number that belongs to a
/// different channel, so the reply lands nowhere and the conversation dies; a blank message; a second chat session
/// opened for a conversation that already had one, so the model answers with no memory of what it already said; a
/// transcript that records an opening line the carrier never accepted; and a text sent to somebody who has asked
/// not to be texted.
/// </summary>
public sealed class SmsOmnichannelProcessorTests
{
    private static readonly DateTime _now = new(2026, 7, 16, 20, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task StartAsync_WhenTheEndpointIsOnTheActivitysChannel_TextsThePreferredDestinationFromThatNumber()
    {
        // Arrange
        var activity = CreateActivity();
        var harness = CreateHarness(activity);

        // Rendered with padding because the opening line is authored by a human in a Liquid template, and templates
        // carry stray newlines around their tags. The padding must not reach the carrier.
        harness.Liquid.Rendered = "  Hi Dana, following up on your quote request.  ";

        // Act
        await harness.CreateProcessor().StartAsync(activity, TestContext.Current.CancellationToken);

        // Assert
        var message = Assert.Single(harness.Sms.Sent);

        Assert.Equal("+15555550100", message.To);
        Assert.Equal("+15550001111", message.From);
        Assert.Equal("Hi Dana, following up on your quote request.", message.Body);
    }

    [Fact]
    public async Task StartAsync_WhenTheEndpointBelongsToAnotherChannel_NeverSendsFromThatNumber()
    {
        // Arrange
        // An activity carries an endpoint identifier, not a channel-checked number. If the endpoint it names has
        // since been repointed at voice, using its value as the SMS sender would text the customer from a number
        // that cannot receive texts: their reply is delivered to a phone line and the conversation is lost. The
        // number must be refused, not substituted.
        var activity = CreateActivity();
        var harness = CreateHarness(activity);

        harness.Endpoints.Add(new OmnichannelChannelEndpoint
        {
            ItemId = "endpoint-sms",
            Channel = OmnichannelConstants.Channels.Phone,
            Value = "+19998887777",
        });

        // Act
        await harness.CreateProcessor().StartAsync(activity, TestContext.Current.CancellationToken);

        // Assert
        var message = Assert.Single(harness.Sms.Sent);

        Assert.Null(message.From);
        Assert.Equal("+15555550100", message.To);
    }

    [Fact]
    public async Task StartAsync_WhenTheEndpointNoLongerExists_StillTextsThePreferredDestinationWithNoFrom()
    {
        // Arrange
        // A deleted endpoint must leave the sender unset so the tenant default applies, rather than leaving a
        // half-built message behind or reaching for some other number.
        var activity = CreateActivity();
        var harness = CreateHarness(activity);

        harness.Endpoints.Clear();

        // Act
        await harness.CreateProcessor().StartAsync(activity, TestContext.Current.CancellationToken);

        // Assert
        var message = Assert.Single(harness.Sms.Sent);

        Assert.Null(message.From);
        Assert.Equal("+15555550100", message.To);
    }

    [Fact]
    public async Task StartAsync_WhenTheActivityNamesNoEndpoint_DoesNotLookOneUp()
    {
        // Arrange
        var activity = CreateActivity();
        activity.ChannelEndpointId = null;

        var harness = CreateHarness(activity);

        // Act
        await harness.CreateProcessor().StartAsync(activity, TestContext.Current.CancellationToken);

        // Assert
        var message = Assert.Single(harness.Sms.Sent);

        Assert.Null(message.From);
        Assert.Equal(0, harness.Endpoints.LookupCount);
    }

    [Fact]
    public async Task StartAsync_WhenTheRenderedInitialPromptIsBlank_ThrowsAndSendsNothing()
    {
        // Arrange
        // A template whose every branch is false renders to whitespace. Sending that texts the customer an empty
        // message from a number they do not recognise, and then waits for a reply to nothing.
        var activity = CreateActivity();
        var harness = CreateHarness(activity);

        harness.Liquid.Rendered = "   \t  ";

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => harness.CreateProcessor().StartAsync(activity, TestContext.Current.CancellationToken));

        // Assert
        Assert.Empty(harness.Sms.Sent);
        Assert.Empty(harness.Prompts.Created);
        Assert.Empty(harness.Sessions.Saved);
        Assert.Equal(ActivityStatus.NotStated, activity.Status);
    }

    [Fact]
    public async Task StartAsync_WhenTheProfileIsNotAChatProfile_ThrowsBeforeRenderingOrSending()
    {
        // Arrange
        // A utility or agent profile has no conversation behind it, so an SMS thread opened against one would have
        // nothing to answer the customer's reply. The mistake has to be refused before a message leaves.
        var activity = CreateActivity();
        var harness = CreateHarness(activity);

        harness.Profiles.Add(CreateChatProfile(type: AIProfileType.Utility));

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => harness.CreateProcessor().StartAsync(activity, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(0, harness.Liquid.RenderCount);
        Assert.Empty(harness.Sms.Sent);
        Assert.Empty(harness.Prompts.Created);
    }

    [Fact]
    public async Task StartAsync_WhenTheProfileHasNoInitialPrompt_ThrowsBeforeRenderingOrSending()
    {
        // Arrange
        // Without an initial prompt there is nothing to open the conversation with, and the customer would receive
        // a text with no content at all.
        var activity = CreateActivity();
        var harness = CreateHarness(activity);

        harness.Profiles.Add(CreateChatProfile(initialPrompt: "   "));

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => harness.CreateProcessor().StartAsync(activity, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(0, harness.Liquid.RenderCount);
        Assert.Empty(harness.Sms.Sent);
        Assert.Empty(harness.Prompts.Created);
    }

    [Fact]
    public async Task StartAsync_WhenNoFlowSettingsAreConfiguredForTheSubject_ThrowsBeforeSending()
    {
        // Arrange
        // The subject flow is where the profile, the goal and the timeout come from. Proceeding without it would
        // text the customer with whatever defaults happened to be lying around.
        var activity = CreateActivity();
        var harness = CreateHarness(activity);

        harness.FlowSettings.Clear();

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => harness.CreateProcessor().StartAsync(activity, TestContext.Current.CancellationToken));

        // Assert
        Assert.Empty(harness.Sms.Sent);
    }

    [Fact]
    public async Task StartAsync_WhenTheProfileCannotBeFound_ThrowsBeforeSending()
    {
        // Arrange
        var activity = CreateActivity();
        var harness = CreateHarness(activity);

        harness.Profiles.Clear();

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => harness.CreateProcessor().StartAsync(activity, TestContext.Current.CancellationToken));

        // Assert
        Assert.Empty(harness.Sms.Sent);
    }

    [Fact]
    public async Task StartAsync_ExposesTheActivityContactFlowSettingsProfileAndSessionToTheTemplate()
    {
        // Arrange
        // The opening line is authored as a Liquid template by whoever set the campaign up, and these five names
        // are the vocabulary they write against. Dropping one does not fail the render -- Liquid resolves the
        // unknown name to nothing -- so the customer silently receives "Hi ," instead of their name.
        var activity = CreateActivity();
        var harness = CreateHarness(activity);
        var contact = harness.Contacts[activity.ContactContentItemId];

        // Act
        await harness.CreateProcessor().StartAsync(activity, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            new[] { "Activity", "Contact", "FlowSettings", "Profile", "Session" },
            harness.Liquid.Properties.Keys.OrderBy(key => key, StringComparer.Ordinal));

        Assert.Same(activity, harness.Liquid.Properties["Activity"].ToObjectValue());
        Assert.Same(contact, harness.Liquid.Properties["Contact"].ToObjectValue());
        Assert.Same(harness.FlowSettings.Find(activity.SubjectContentType), harness.Liquid.Properties["FlowSettings"].ToObjectValue());
        Assert.Same(harness.Profiles.Find("profile-1"), harness.Liquid.Properties["Profile"].ToObjectValue());
        Assert.Same(Assert.Single(harness.Sessions.Saved), harness.Liquid.Properties["Session"].ToObjectValue());
    }

    [Fact]
    public async Task StartAsync_WhenTheActivityNamesAResolvableCampaign_AddsItToTheTemplateContext()
    {
        // Arrange
        var activity = CreateActivity();
        activity.CampaignId = "campaign-1";

        var harness = CreateHarness(activity);
        var campaign = new OmnichannelCampaign
        {
            ItemId = "campaign-1",
            DisplayText = "Spring quotes",
        };

        harness.Campaigns.Add(campaign);

        // Act
        await harness.CreateProcessor().StartAsync(activity, TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(campaign, harness.Liquid.Properties["Campaign"].ToObjectValue());
    }

    [Fact]
    public async Task StartAsync_WhenTheNamedCampaignNoLongerExists_LeavesCampaignOutOfTheTemplateContext()
    {
        // Arrange
        // A campaign deleted after the batch was loaded leaves the identifier behind on the activity. Publishing
        // the key anyway would hand the template a wrapper around nothing, and "{{ Campaign.DisplayText }}" would
        // read as a blank rather than being skippable with an "{% if Campaign %}" the author already wrote.
        var activity = CreateActivity();
        activity.CampaignId = "campaign-gone";

        var harness = CreateHarness(activity);

        // Act
        await harness.CreateProcessor().StartAsync(activity, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(harness.Liquid.Properties.ContainsKey("Campaign"));
        Assert.Single(harness.Sms.Sent);
    }

    [Fact]
    public async Task StartAsync_WhenTheActivityAlreadyHasASession_ReusesItInsteadOfStartingASecond()
    {
        // Arrange
        // An activity can reach this code twice: the periodic pass and the "Place Call or Send Message" workflow
        // task both start activities, and a failed send is retried. Opening a second chat session would strand the
        // first transcript, and every later reply would be answered by a model that cannot see what was already
        // said to this customer.
        var activity = CreateActivity();
        activity.AISessionId = "session-1";

        var harness = CreateHarness(activity);
        var existing = new AIChatSession
        {
            SessionId = "session-1",
            ProfileId = "profile-1",
            CreatedUtc = _now.AddDays(-1),
            LastActivityUtc = _now.AddDays(-1),
        };

        harness.Sessions.Seed(existing);

        // Act
        await harness.CreateProcessor().StartAsync(activity, TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(existing, Assert.Single(harness.Sessions.All));
        Assert.Same(existing, Assert.Single(harness.Sessions.Saved));
        Assert.Equal("session-1", activity.AISessionId);
        Assert.Equal("session-1", Assert.Single(harness.Prompts.Created).SessionId);

        // The reused session is the one the model will be resumed from, so its clock has to move forward too.
        Assert.Equal(_now, existing.LastActivityUtc);
        Assert.Equal(_now.AddDays(-1), existing.CreatedUtc);
    }

    [Fact]
    public async Task StartAsync_WhenTheActivityHasNoSession_OpensOneForTheProfileAndRecordsItOnTheActivity()
    {
        // Arrange
        var activity = CreateActivity();
        var harness = CreateHarness(activity);

        // Act
        await harness.CreateProcessor().StartAsync(activity, TestContext.Current.CancellationToken);

        // Assert
        var session = Assert.Single(harness.Sessions.Saved);

        Assert.False(string.IsNullOrEmpty(session.SessionId));
        Assert.Equal("profile-1", session.ProfileId);
        Assert.Equal(_now, session.CreatedUtc);
        Assert.Equal(_now, session.LastActivityUtc);
        Assert.Equal("Automated SMS Activity", session.Title);

        // Without this the next inbound message cannot find the conversation it belongs to.
        Assert.Equal(session.SessionId, activity.AISessionId);
    }

    [Fact]
    public async Task StartAsync_WhenTheSendSucceeds_StampsTheOpeningMessageWithTheClockAndAwaitsTheCustomer()
    {
        // Arrange
        // The opening line has to be stored with the time it was said. Left unstamped it defaults to
        // DateTime.MinValue and sorts ahead of every later message, which corrupts the owed-reply scan and the
        // transcript for the rest of the conversation.
        var activity = CreateActivity();
        var harness = CreateHarness(activity);

        harness.Liquid.Rendered = "Hi Dana, following up on your quote request.";

        // Act
        await harness.CreateProcessor().StartAsync(activity, TestContext.Current.CancellationToken);

        // Assert
        var prompt = Assert.Single(harness.Prompts.Created);

        Assert.Equal(ChatRole.Assistant, prompt.Role);
        Assert.Equal("Hi Dana, following up on your quote request.", prompt.Content);
        Assert.Equal(_now, prompt.CreatedUtc);
        Assert.False(string.IsNullOrEmpty(prompt.ItemId));
        Assert.Equal(activity.AISessionId, prompt.SessionId);

        Assert.Equal(ActivityStatus.AwaitingCustomerAnswer, activity.Status);

        // The subject flow in the ordinary case defines no timeout, so the schedule must be left exactly as loaded.
        Assert.Equal(_now.AddHours(-1), activity.ScheduledUtc);
    }

    [Fact]
    public async Task StartAsync_WhenTheFlowDefinesANoResponseTimeout_SchedulesTheDeadlineFromTheClock()
    {
        // Arrange
        var activity = CreateActivity();
        var harness = CreateHarness(activity);

        harness.FlowSettings.Add(new SubjectFlowSettings
        {
            SubjectContentType = activity.SubjectContentType,
            Channel = OmnichannelConstants.Channels.Sms,
            ProfileId = "profile-1",
            NoResponseTimeoutInMinutes = 45,
        });

        // Act
        await harness.CreateProcessor().StartAsync(activity, TestContext.Current.CancellationToken);

        // Assert
        // Measured from when the message actually went out, not from when the activity was originally due.
        Assert.Equal(_now.AddMinutes(45), activity.ScheduledUtc);
    }

    [Fact]
    public async Task StartAsync_WhenTheProviderRejectsTheSend_ThrowsAndRecordsNothingAsSaid()
    {
        // Arrange
        // A rejected send means the customer never heard from us. Persisting the opening line anyway would put a
        // message in the transcript that was never delivered, and moving the activity to "awaiting answer" would
        // leave it waiting forever for a reply to something nobody received.
        var activity = CreateActivity();
        var harness = CreateHarness(activity);

        harness.Sms.ProviderAccepts = false;

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => harness.CreateProcessor().StartAsync(activity, TestContext.Current.CancellationToken));

        // Assert
        Assert.Empty(harness.Prompts.Created);
        Assert.Empty(harness.Sessions.Saved);
        Assert.Null(activity.AISessionId);
        Assert.Equal(ActivityStatus.NotStated, activity.Status);
    }

    [Fact]
    public async Task StartAsync_WhenTheActivityNamesItsOwnProfile_PrefersItOverTheSubjectFlowDefault()
    {
        // Arrange
        // A batch can be loaded against a profile chosen for that batch, which must beat the subject-wide default;
        // otherwise every campaign speaks with the same voice regardless of what was configured for it.
        var activity = CreateActivity();
        activity.AIProfileId = "profile-batch";

        var harness = CreateHarness(activity);

        harness.Profiles.Add(CreateChatProfile(itemId: "profile-batch", initialPrompt: "Batch opener"));

        // Act
        await harness.CreateProcessor().StartAsync(activity, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("Batch opener", harness.Liquid.Template);
        Assert.Equal("profile-batch", Assert.Single(harness.Sessions.Saved).ProfileId);
    }

    [Fact]
    public async Task StartAsync_WhenTheContactHasAskedNotToBeTexted_SendsNothing()
    {
        // Arrange
        // The preference is read when the batch is loaded, and the activity comes due hours later. Somebody who
        // opts out in between is still holding an activity with their number on it.
        //
        // The periodic pass screens for this one level up, but it is not the only caller: the "Place Call or Send
        // Message" workflow task calls StartAsync directly and screens nothing, and a retry re-enters here too.
        // This method is the last code before the carrier, and it already loads the contact for the template -- so
        // the question costs nothing to ask here and asking it is what makes the guarantee hold for every caller
        // rather than for one of them.
        var activity = CreateActivity();
        var harness = CreateHarness(activity);

        harness.Contacts[activity.ContactContentItemId] = CreateContact(activity.ContactContentItemId, doNotSms: true);

        // Act
        // Refusing by throwing and refusing by returning quietly are both defensible; not sending is not optional.
        _ = await Record.ExceptionAsync(
            () => harness.CreateProcessor().StartAsync(activity, TestContext.Current.CancellationToken));

        // Assert
        Assert.Empty(harness.Sms.Sent);
        Assert.Empty(harness.Prompts.Created);
        Assert.NotEqual(ActivityStatus.AwaitingCustomerAnswer, activity.Status);
    }

    private static OmnichannelActivity CreateActivity()
    {
        return new OmnichannelActivity
        {
            ItemId = "activity-1",
            Channel = OmnichannelConstants.Channels.Sms,
            ChannelEndpointId = "endpoint-sms",
            ContactContentItemId = "contact-1",
            ContactContentType = "Lead",
            SubjectContentType = "LeadFollowUp",
            PreferredDestination = "+15555550100",
            InteractionType = ActivityInteractionType.Automated,
            Status = ActivityStatus.NotStated,
            ScheduledUtc = _now.AddHours(-1),
            CreatedUtc = _now.AddHours(-1),
        };
    }

    private static AIProfile CreateChatProfile(
        string itemId = "profile-1",
        string initialPrompt = "Hi {{ Contact.DisplayText }}",
        AIProfileType type = AIProfileType.Chat)
    {
        var profile = new AIProfile
        {
            ItemId = itemId,
            Name = "sms-opener",
            DisplayText = "SMS opener",
            Type = type,
        };

        profile.Put(new AIProfileMetadata
        {
            InitialPrompt = initialPrompt,
        });

        return profile;
    }

    private static ContentItem CreateContact(string contentItemId, bool doNotSms = false)
    {
        var contact = new ContentItem
        {
            ContentItemId = contentItemId,
            ContentType = "Lead",
            DisplayText = "Dana Reyes",
        };

        if (doNotSms)
        {
            contact.Alter<OmnichannelContactPart>(part => part.SetDoNotSms(true, _now));
        }

        return contact;
    }

    /// <summary>
    /// The ordinary case, which every test starts from: a subject flow naming a chat profile that has an opening
    /// prompt, an SMS endpoint on the activity's own channel, and a contact who has not asked to be left alone.
    /// A test replaces only the one thing it is about.
    /// </summary>
    private static Harness CreateHarness(OmnichannelActivity activity)
    {
        var harness = new Harness();

        harness.Profiles.Add(CreateChatProfile());

        harness.FlowSettings.Add(new SubjectFlowSettings
        {
            SubjectContentType = activity.SubjectContentType,
            Channel = OmnichannelConstants.Channels.Sms,
            ProfileId = "profile-1",
        });

        harness.Endpoints.Add(new OmnichannelChannelEndpoint
        {
            ItemId = "endpoint-sms",
            Channel = OmnichannelConstants.Channels.Sms,
            Value = "+15550001111",
        });

        harness.Contacts[activity.ContactContentItemId] = CreateContact(activity.ContactContentItemId);

        return harness;
    }

    /// <summary>
    /// Everything the processor talks to, wired together and inspectable after the act.
    /// </summary>
    private sealed class Harness
    {
        public FakeChatSessionManager Sessions { get; } = new();

        public FakePromptStore Prompts { get; } = new();

        public FakeProfileManager Profiles { get; } = new();

        public FakeCatalog<OmnichannelCampaign> Campaigns { get; } = new();

        public FakeCatalog<OmnichannelChannelEndpoint> Endpoints { get; } = new();

        public FakeSubjectFlowSettingsService FlowSettings { get; } = new();

        public RecordingSmsService Sms { get; } = new();

        public CapturingLiquidTemplateManager Liquid { get; } = new();

        public Dictionary<string, ContentItem> Contacts { get; } = new(StringComparer.Ordinal);

        public SmsOmnichannelProcessor CreateProcessor()
        {
            var contentManager = new Mock<IContentManager>();

            contentManager
                .Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<VersionOptions>()))
                .ReturnsAsync((string contentItemId, VersionOptions _) =>
                {
                    if (contentItemId is not null && Contacts.TryGetValue(contentItemId, out var contact))
                    {
                        return contact;
                    }

                    return null;
                });

            return new SmsOmnichannelProcessor(
                Sessions,
                Prompts,
                Profiles,
                Campaigns,
                FlowSettings,
                Endpoints,
                Sms,
                Liquid,
                contentManager.Object,
                new FakeTimeProvider(_now),
                new PassThroughStringLocalizer<SmsOmnichannelProcessor>());
        }
    }

    /// <summary>
    /// An SMS service that keeps what it was handed instead of talking to a carrier, and can be told to reject.
    /// </summary>
    private sealed class RecordingSmsService : ISmsService
    {
        public List<SmsMessage> Sent { get; } = [];

        public bool ProviderAccepts { get; set; } = true;

        public Task<Result> SendAsync(SmsMessage message, CancellationToken cancellationToken = default)
        {
            Sent.Add(message);

            return Task.FromResult(ProviderAccepts
                ? Result.Success()
                : Result.Failed(new ResultError { Message = new LocalizedString("carrier-rejected", "The carrier rejected the message.") }));
        }
    }

    /// <summary>
    /// A template manager that keeps the template and the names published to it, and renders whatever it is told
    /// to render. The rendering itself belongs to OrchardCore; what this file is about is which names the
    /// processor puts within reach of whoever authored the template.
    /// </summary>
    private sealed class CapturingLiquidTemplateManager : ILiquidTemplateManager
    {
        public string Rendered { get; set; } = "Hi Dana, following up on your quote request.";

        public string Template { get; private set; }

        public IReadOnlyDictionary<string, FluidValue> Properties { get; private set; } =
            new Dictionary<string, FluidValue>(StringComparer.Ordinal);

        public int RenderCount { get; private set; }

        public Task<string> RenderStringAsync(
            string template,
            TextEncoder encoder,
            object model,
            IEnumerable<KeyValuePair<string, FluidValue>> properties)
        {
            RenderCount++;
            Template = template;
            Properties = properties is null
                ? new Dictionary<string, FluidValue>(StringComparer.Ordinal)
                : properties.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);

            return Task.FromResult(Rendered);
        }

        public Task<IHtmlContent> RenderHtmlContentAsync(
            string template,
            TextEncoder encoder,
            object model,
            IEnumerable<KeyValuePair<string, FluidValue>> properties)
            => throw new NotSupportedException();

        public Task RenderAsync(
            string template,
            TextWriter writer,
            TextEncoder encoder,
            object model,
            IEnumerable<KeyValuePair<string, FluidValue>> properties)
            => throw new NotSupportedException();

        public bool Validate(string template, out IEnumerable<string> errors)
        {
            errors = Array.Empty<string>();

            return true;
        }
    }

    /// <summary>
    /// A chat session manager backed by a dictionary, so "was a second session opened?" is a question a test can
    /// actually ask.
    /// </summary>
    private sealed class FakeChatSessionManager : IAIChatSessionManager
    {
        private readonly Dictionary<string, AIChatSession> _sessions = new(StringComparer.Ordinal);

        public List<AIChatSession> Saved { get; } = [];

        public IReadOnlyCollection<AIChatSession> All => _sessions.Values;

        public void Seed(AIChatSession session)
            => _sessions[session.SessionId] = session;

        public Task<AIChatSession> FindByIdAsync(string id, CancellationToken cancellationToken = default)
        {
            if (id is not null && _sessions.TryGetValue(id, out var session))
            {
                return Task.FromResult(session);
            }

            return Task.FromResult<AIChatSession>(null);
        }

        public Task<AIChatSession> FindAsync(string id, CancellationToken cancellationToken = default)
            => FindByIdAsync(id, cancellationToken);

        public Task SaveAsync(AIChatSession chatSession, CancellationToken cancellationToken = default)
        {
            _sessions[chatSession.SessionId] = chatSession;
            Saved.Add(chatSession);

            return Task.CompletedTask;
        }

        public Task<AIChatSessionResult> PageAsync(
            int page,
            int pageSize,
            AIChatSessionQueryContext context = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<AIChatSession> NewAsync(
            AIProfile profile,
            NewAIChatSessionContext context,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> DeleteAsync(string sessionId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<int> DeleteAllAsync(string profileId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    /// <summary>
    /// A prompt store that keeps what was written to it, so a test can ask what the transcript now claims we said.
    /// </summary>
    private sealed class FakePromptStore : IAIChatSessionPromptStore
    {
        private readonly List<AIChatSessionPrompt> _prompts = [];

        public IReadOnlyList<AIChatSessionPrompt> Created => _prompts;

        public ValueTask CreateAsync(AIChatSessionPrompt entry, CancellationToken cancellationToken = default)
        {
            _prompts.Add(entry);

            return ValueTask.CompletedTask;
        }

        public ValueTask UpdateAsync(AIChatSessionPrompt entry, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public ValueTask<bool> DeleteAsync(AIChatSessionPrompt entry, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(_prompts.Remove(entry));

        public ValueTask<AIChatSessionPrompt> FindByIdAsync(string id, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(_prompts.Find(prompt => string.Equals(prompt.ItemId, id, StringComparison.Ordinal)));

        public ValueTask<IReadOnlyCollection<AIChatSessionPrompt>> GetAllAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IReadOnlyCollection<AIChatSessionPrompt>>(_prompts);

        public ValueTask<IReadOnlyCollection<AIChatSessionPrompt>> GetAsync(
            IEnumerable<string> ids,
            CancellationToken cancellationToken = default)
        {
            var wanted = new HashSet<string>(ids, StringComparer.Ordinal);

            return ValueTask.FromResult<IReadOnlyCollection<AIChatSessionPrompt>>(
                _prompts.Where(prompt => wanted.Contains(prompt.ItemId)).ToList());
        }

        public ValueTask<PageResult<AIChatSessionPrompt>> PageAsync<TQuery>(
            int page,
            int pageSize,
            TQuery context,
            CancellationToken cancellationToken = default)
            where TQuery : QueryContext
            => throw new NotSupportedException();

        public Task<IReadOnlyList<AIChatSessionPrompt>> GetPromptsAsync(string sessionId)
            => Task.FromResult<IReadOnlyList<AIChatSessionPrompt>>(
                _prompts
                    .Where(prompt => string.Equals(prompt.SessionId, sessionId, StringComparison.Ordinal))
                    .OrderBy(prompt => prompt.CreatedUtc)
                    .ToList());

        public Task<int> DeleteAllPromptsAsync(string sessionId)
            => Task.FromResult(
                _prompts.RemoveAll(prompt => string.Equals(prompt.SessionId, sessionId, StringComparison.Ordinal)));

        public Task<int> CountAsync(string sessionId)
            => Task.FromResult(
                _prompts.Count(prompt => string.Equals(prompt.SessionId, sessionId, StringComparison.Ordinal)));
    }

    /// <summary>
    /// A catalog backed by a dictionary that also counts lookups, so a test can tell "not found" from "never
    /// asked".
    /// </summary>
    private sealed class FakeCatalog<T> : ICatalog<T>
        where T : CatalogItem
    {
        private readonly Dictionary<string, T> _entries = new(StringComparer.Ordinal);

        public int LookupCount { get; private set; }

        public void Add(T entry)
            => _entries[entry.ItemId] = entry;

        public void Clear()
            => _entries.Clear();

        public ValueTask<T> FindByIdAsync(string id, CancellationToken cancellationToken = default)
        {
            LookupCount++;

            if (id is not null && _entries.TryGetValue(id, out var entry))
            {
                return ValueTask.FromResult(entry);
            }

            return ValueTask.FromResult<T>(null);
        }

        public ValueTask<IReadOnlyCollection<T>> GetAllAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IReadOnlyCollection<T>>(_entries.Values.ToList());

        public ValueTask<IReadOnlyCollection<T>> GetAsync(
            IEnumerable<string> ids,
            CancellationToken cancellationToken = default)
        {
            var wanted = new HashSet<string>(ids, StringComparer.Ordinal);

            return ValueTask.FromResult<IReadOnlyCollection<T>>(
                _entries.Values.Where(entry => wanted.Contains(entry.ItemId)).ToList());
        }

        public ValueTask<PageResult<T>> PageAsync<TQuery>(
            int page,
            int pageSize,
            TQuery context,
            CancellationToken cancellationToken = default)
            where TQuery : QueryContext
            => throw new NotSupportedException();

        public ValueTask<bool> DeleteAsync(T entry, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(_entries.Remove(entry.ItemId));

        public ValueTask CreateAsync(T entry, CancellationToken cancellationToken = default)
        {
            Add(entry);

            return ValueTask.CompletedTask;
        }

        public ValueTask UpdateAsync(T entry, CancellationToken cancellationToken = default)
        {
            Add(entry);

            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// A profile manager backed by a dictionary, so "which profile was chosen?" is answerable.
    /// </summary>
    private sealed class FakeProfileManager : IAIProfileManager
    {
        private readonly Dictionary<string, AIProfile> _profiles = new(StringComparer.Ordinal);

        public void Add(AIProfile profile)
            => _profiles[profile.ItemId] = profile;

        public void Clear()
            => _profiles.Clear();

        public AIProfile Find(string itemId)
            => _profiles.GetValueOrDefault(itemId);

        public ValueTask<AIProfile> FindByIdAsync(string id, CancellationToken cancellationToken = default)
        {
            if (id is not null && _profiles.TryGetValue(id, out var profile))
            {
                return ValueTask.FromResult(profile);
            }

            return ValueTask.FromResult<AIProfile>(null);
        }

        public ValueTask<AIProfile> FindByNameAsync(string name, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(
                _profiles.Values.FirstOrDefault(profile => string.Equals(profile.Name, name, StringComparison.Ordinal)));

        public ValueTask<IEnumerable<AIProfile>> GetAllAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IEnumerable<AIProfile>>(_profiles.Values.ToList());

        public ValueTask<IEnumerable<AIProfile>> GetAsync(
            AIProfileType type,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IEnumerable<AIProfile>>(
                _profiles.Values.Where(profile => profile.Type == type).ToList());

        public ValueTask<PageResult<AIProfile>> PageAsync<TQuery>(
            int page,
            int pageSize,
            TQuery context,
            CancellationToken cancellationToken = default)
            where TQuery : QueryContext
            => throw new NotSupportedException();

        public ValueTask<bool> DeleteAsync(AIProfile model, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(_profiles.Remove(model.ItemId));

        public ValueTask<AIProfile> NewAsync(JsonNode data = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public ValueTask<AIProfile> NewAsync(
            string name,
            JsonNode data = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public ValueTask CreateAsync(AIProfile model, CancellationToken cancellationToken = default)
        {
            Add(model);

            return ValueTask.CompletedTask;
        }

        public ValueTask UpdateAsync(
            AIProfile model,
            JsonNode data = null,
            CancellationToken cancellationToken = default)
        {
            Add(model);

            return ValueTask.CompletedTask;
        }

        public ValueTask<ValidationResultDetails> ValidateAsync(
            AIProfile model,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    /// <summary>
    /// A subject flow settings service backed by a dictionary keyed on the subject content type.
    /// </summary>
    private sealed class FakeSubjectFlowSettingsService : ISubjectFlowSettingsService
    {
        private readonly Dictionary<string, SubjectFlowSettings> _settings = new(StringComparer.Ordinal);

        public void Add(SubjectFlowSettings settings)
            => _settings[settings.SubjectContentType] = settings;

        public void Clear()
            => _settings.Clear();

        public SubjectFlowSettings Find(string subjectContentType)
            => _settings.GetValueOrDefault(subjectContentType);

        public Task<IReadOnlyList<SubjectFlowSettings>> GetConfiguredFlowSettingsAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SubjectFlowSettings>>(_settings.Values.ToList());

        public Task<SubjectFlowSettings> FindConfiguredFlowSettingsAsync(
            string subjectContentType,
            CancellationToken cancellationToken = default)
        {
            if (subjectContentType is not null && _settings.TryGetValue(subjectContentType, out var settings))
            {
                return Task.FromResult(settings);
            }

            return Task.FromResult<SubjectFlowSettings>(null);
        }

        public Task<IReadOnlyList<SubjectDefinition>> GetConfiguredSubjectDefinitionsAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SubjectDefinition>>([]);

        public Task<IReadOnlyList<SubjectDefinition>> GetConfiguredSubjectDefinitionsAsync(
            SubjectDirection direction,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SubjectDefinition>>([]);

        public bool IsConfigured(SubjectFlowSettings flowSettings)
            => flowSettings is not null;
    }
}
