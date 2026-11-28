using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Consent;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.Tests.Agent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.AITools;

/// <summary>
/// The services the subject flow, not-in-service, AI call, address and CRM tools need, with in-memory catalog managers
/// and doubles of the other services.
/// </summary>
internal sealed class ManagementToolHarness
{
    public static readonly DateTime Now = new(2026, 11, 18, 15, 0, 0, DateTimeKind.Utc);

    public FakeNamedCatalogManager<OmnichannelDisposition> Dispositions { get; } = new();

    public FakeNamedCatalogManager<LeadStatus> LeadStatuses { get; } = new();

    public FakeNamedCatalogManager<OpportunityStage> OpportunityStages { get; } = new();

    public FakeCatalogManager<OmnichannelChannelEndpoint> Endpoints { get; } = new();

    public FakeCatalogManager<OmnichannelCampaign> Campaigns { get; } = new();

    public List<SubjectAction> SubjectActions { get; } = [];

    public List<SubjectAction> CreatedActions { get; } = [];

    public Func<SubjectAction, ValidationResultDetails> ValidateAction { get; set; } = _ => new ValidationResultDetails();

    public Dictionary<string, ContentTypeDefinition> ContentTypes { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, ContentItem> ContentItems { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, OmnichannelActivity> Activities { get; } = new(StringComparer.Ordinal);

    public List<OmnichannelActivity> CreatedActivities { get; } = [];

    public List<OmnichannelActivity> UpdatedActivities { get; } = [];

    public HashSet<string> AIProfileIds { get; } = new(StringComparer.Ordinal) { "profile-1" };

    public HashSet<string> OptedOutAddresses { get; } = new(StringComparer.Ordinal);

    public HashSet<string> DoNotCallListAddresses { get; } = new(StringComparer.Ordinal);

    public List<(string Channel, string Address, ConsentChange Change)> RecordedConsent { get; } = [];

    public Mock<INotInServiceNumberService> NotInService { get; } = new();

    public Mock<ISubjectFlowSettingsService> FlowSettings { get; } = new();

    public Mock<ILeadConversionService> LeadConversion { get; } = new();

    public SubjectActionOptions ActionOptions { get; } = new();

    public OmnichannelAddressOptions AddressOptions { get; } = new();

    public ManagementToolHarness()
    {
        ActionOptions.AddActionType(OmnichannelConstants.ActionTypes.Finish);
        ActionOptions.AddActionType(OmnichannelConstants.ActionTypes.TryAgain);
        ActionOptions.AddActionType(OmnichannelConstants.ActionTypes.NewActivity);

        AddressOptions.AddressTypes[OmnichannelAddressTypes.PhoneNumber] = new OmnichannelAddressType { Name = OmnichannelAddressTypes.PhoneNumber };
        AddressOptions.Capabilities["Phone"] = new OmnichannelAddressCapability { Name = "Phone", AddressType = OmnichannelAddressTypes.PhoneNumber };
        AddressOptions.Capabilities["SMS"] = new OmnichannelAddressCapability { Name = "SMS", AddressType = OmnichannelAddressTypes.PhoneNumber };

        NotInService.Setup(service => service.Normalize(It.IsAny<string>()))
            .Returns((string number) => number is not null && number.StartsWith('+') ? number.Replace(" ", string.Empty, StringComparison.Ordinal) : null);
    }

    public IServiceProvider Build(bool signedIn = true, Func<Permission, object, bool> authorize = null)
    {
        var services = AgentToolTestContext.CreateServices(signedIn, authorize);

        services.AddSingleton<INamedCatalogManager<OmnichannelDisposition>>(Dispositions);
        services.AddSingleton<INamedCatalogManager<LeadStatus>>(LeadStatuses);
        services.AddSingleton<INamedCatalogManager<OpportunityStage>>(OpportunityStages);
        services.AddSingleton<ICatalogManager<OmnichannelChannelEndpoint>>(Endpoints);
        services.AddSingleton(Options.Create(ActionOptions));
        services.AddSingleton(Options.Create(AddressOptions));
        services.AddSingleton(NotInService.Object);
        services.AddSingleton(FlowSettings.Object);
        services.AddSingleton(LeadConversion.Object);
        services.AddSingleton(new LeadStatusFlagService(LeadStatuses));

        var dispositionCatalog = new Mock<INamedCatalog<OmnichannelDisposition>>();
        dispositionCatalog.Setup(catalog => catalog.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string id, CancellationToken _) => ValueTask.FromResult(Dispositions.Items.FirstOrDefault(item => item.ItemId == id)));
        dispositionCatalog.Setup(catalog => catalog.GetAllAsync(It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult<IReadOnlyCollection<OmnichannelDisposition>>(Dispositions.Items.ToList()));
        services.AddSingleton(dispositionCatalog.Object);

        var statusCatalog = new Mock<INamedCatalog<LeadStatus>>();
        statusCatalog.Setup(catalog => catalog.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string id, CancellationToken _) => ValueTask.FromResult(LeadStatuses.Items.FirstOrDefault(item => item.ItemId == id)));
        services.AddSingleton(statusCatalog.Object);

        var campaignCatalog = new Mock<ICatalog<OmnichannelCampaign>>();
        campaignCatalog.Setup(catalog => catalog.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string id, CancellationToken _) => ValueTask.FromResult(Campaigns.Items.FirstOrDefault(item => item.ItemId == id)));
        services.AddSingleton(campaignCatalog.Object);

        var actionCatalog = new Mock<ISourceCatalog<SubjectAction>>();
        actionCatalog.Setup(catalog => catalog.GetAllAsync(It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult<IReadOnlyCollection<SubjectAction>>(SubjectActions.ToList()));
        services.AddSingleton(actionCatalog.Object);

        var actionManager = new Mock<ISourceCatalogManager<SubjectAction>>();
        actionManager.Setup(manager => manager.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string id, CancellationToken _) => ValueTask.FromResult(SubjectActions.FirstOrDefault(action => action.ItemId == id)));
        actionManager.Setup(manager => manager.NewAsync(It.IsAny<string>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .Returns((string source, JsonNode _, CancellationToken _) => ValueTask.FromResult(new SubjectAction { ItemId = "new-action", Source = source }));
        actionManager.Setup(manager => manager.ValidateAsync(It.IsAny<SubjectAction>(), It.IsAny<CancellationToken>()))
            .Returns((SubjectAction action, CancellationToken _) => ValueTask.FromResult(ValidateAction(action)));
        actionManager.Setup(manager => manager.CreateAsync(It.IsAny<SubjectAction>(), It.IsAny<CancellationToken>()))
            .Returns((SubjectAction action, CancellationToken _) =>
            {
                CreatedActions.Add(action);

                return ValueTask.CompletedTask;
            });
        services.AddSingleton(actionManager.Object);

        var definitions = new Mock<IContentDefinitionManager>();
        definitions.Setup(manager => manager.GetTypeDefinitionAsync(It.IsAny<string>()))
            .ReturnsAsync((string name) => ContentTypes.GetValueOrDefault(name));
        services.AddSingleton(definitions.Object);

        var contentManager = new Mock<IContentManager>();
        contentManager.Setup(manager => manager.GetAsync(It.IsAny<string>(), It.IsAny<VersionOptions>()))
            .ReturnsAsync((string id, VersionOptions _) => id is not null && ContentItems.TryGetValue(id, out var item) ? item : null);
        services.AddSingleton(contentManager.Object);

        var activityManager = new Mock<IOmnichannelActivityManager>();
        activityManager.Setup(manager => manager.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string id, CancellationToken _) => ValueTask.FromResult(id is not null && Activities.TryGetValue(id, out var activity) ? activity : null));
        activityManager.Setup(manager => manager.NewAsync(It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult(new OmnichannelActivity { ItemId = "new-activity" }));
        activityManager.Setup(manager => manager.CreateAsync(It.IsAny<OmnichannelActivity>(), It.IsAny<CancellationToken>()))
            .Returns((OmnichannelActivity activity, CancellationToken _) =>
            {
                CreatedActivities.Add(activity);

                return ValueTask.CompletedTask;
            });
        activityManager.Setup(manager => manager.UpdateAsync(It.IsAny<OmnichannelActivity>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .Returns((OmnichannelActivity activity, JsonNode _, CancellationToken _) =>
            {
                UpdatedActivities.Add(activity);

                return ValueTask.CompletedTask;
            });
        services.AddSingleton(activityManager.Object);

        var clock = new Mock<IClock>();
        clock.Setup(value => value.UtcNow).Returns(Now);
        services.AddSingleton(clock.Object);

        services.AddSingleton(new AIActivityStarter(activityManager.Object, contentManager.Object, clock.Object));

        var profiles = new Mock<IAIProfileManager>();
        profiles.Setup(manager => manager.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string id, CancellationToken _) => ValueTask.FromResult(AIProfileIds.Contains(id) ? new AIProfile { ItemId = id } : null));
        services.AddSingleton(profiles.Object);

        var optOuts = new Mock<IContactOptOutResolver>();
        optOuts.Setup(resolver => resolver.HasAddressOptedOutAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string address, CancellationToken _) => OptedOutAddresses.Contains(address));
        services.AddSingleton(optOuts.Object);

        // The consent service answers from the address opt-outs and the do-not-call lists the test sets, and keeps
        // what is recorded through it.
        var consent = new Mock<IConsentService>();
        consent.Setup(service => service.GetAsync(It.IsAny<ConsentQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConsentQuery query, CancellationToken _) =>
                query.Address is not null && OptedOutAddresses.Contains(query.Address)
                    ? ConsentStatus.OptedOutBy(ConsentSourceNames.ContactPreferences)
                    : query.Address is not null && DoNotCallListAddresses.Contains(query.Address)
                        ? ConsentStatus.OptedOutBy(ConsentSourceNames.DoNotCallLists)
                        : ConsentStatus.NotOptedOut);
        consent.Setup(service => service.RecordAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ConsentChange>(), It.IsAny<CancellationToken>()))
            .Returns((string channel, string address, ConsentChange change, CancellationToken _) =>
            {
                RecordedConsent.Add((channel, address, change));

                return Task.CompletedTask;
            });
        services.AddSingleton(consent.Object);

        return services.BuildServiceProvider();
    }

    public void AddSubjectType(string name = "SalesCall", string displayName = "Sales Call")
        => ContentTypes[name] = new ContentTypeDefinition(
            name,
            displayName,
            [new ContentTypePartDefinition(
                OmnichannelConstants.ContentParts.OmnichannelSubject,
                new ContentPartDefinition(OmnichannelConstants.ContentParts.OmnichannelSubject),
                new JsonObject())],
            []);

    public static ValidationResultDetails Fail(string message)
    {
        var validation = new ValidationResultDetails();
        validation.Fail(new ValidationResult(message));

        return validation;
    }
}
