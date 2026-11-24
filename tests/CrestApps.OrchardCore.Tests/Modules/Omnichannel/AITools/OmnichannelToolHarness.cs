using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.Tests.Agent;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Environment.Shell.Descriptor.Models;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.AITools;

/// <summary>
/// The services the omnichannel tools need, with in-memory catalog managers and doubles of the activity services.
/// </summary>
internal sealed class OmnichannelToolHarness
{
    public static readonly DateTime Now = new(2026, 11, 17, 15, 0, 0, DateTimeKind.Utc);

    public FakeCatalogManager<OmnichannelCampaign> Campaigns { get; } = new();

    public FakeCatalogManager<OmnichannelCampaignGroup> CampaignGroups { get; } = new();

    public FakeNamedCatalogManager<OmnichannelDisposition> Dispositions { get; } = new();

    public FakeCatalogManager<Cadence> Cadences { get; } = new();

    public FakeNamedCatalogManager<LeadStatus> LeadStatuses { get; } = new();

    public FakeNamedCatalogManager<OpportunityStage> OpportunityStages { get; } = new();

    public FakeCatalogManager<OmnichannelActivityBatch> Batches { get; } = new();

    public List<SubjectAction> SubjectActions { get; } = [];

    public Dictionary<string, OmnichannelActivity> Activities { get; } = new(StringComparer.Ordinal);

    public List<OmnichannelActivity> UpdatedActivities { get; } = [];

    public Dictionary<string, ContentItem> Contacts { get; } = new(StringComparer.Ordinal);

    public Mock<IOmnichannelActivityManager> ActivityManager { get; } = new();

    public Mock<IActivityDispositionService> DispositionService { get; } = new();

    public Mock<IActivityBatchLoadCoordinator> LoadCoordinator { get; } = new();

    public Mock<IActivityDialerContributor> Dialer { get; } = new();

    public HashSet<string> InFlight { get; } = new(StringComparer.Ordinal);

    public bool CrmEnabled { get; set; } = true;

    public OmnichannelToolHarness()
    {
        ActivityManager.Setup(manager => manager.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string id, CancellationToken _) => ValueTask.FromResult(id is not null && Activities.TryGetValue(id, out var activity) ? activity : null));
        ActivityManager.Setup(manager => manager.UpdateAsync(It.IsAny<OmnichannelActivity>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .Returns((OmnichannelActivity activity, JsonNode _, CancellationToken _) =>
            {
                UpdatedActivities.Add(activity);

                return ValueTask.CompletedTask;
            });

        Dialer.Setup(dialer => dialer.GetActivitiesInFlightAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<string> ids, CancellationToken _) => (IReadOnlySet<string>)ids.Where(InFlight.Contains).ToHashSet(StringComparer.Ordinal));
    }

    public IServiceProvider Build(bool signedIn = true, Func<Permission, object, bool> authorize = null)
    {
        var services = AgentToolTestContext.CreateServices(signedIn, authorize);

        services.AddSingleton<ICatalogManager<OmnichannelCampaign>>(Campaigns);
        services.AddSingleton<ICatalogManager<OmnichannelCampaignGroup>>(CampaignGroups);
        services.AddSingleton<INamedCatalogManager<OmnichannelDisposition>>(Dispositions);
        services.AddSingleton<ICatalogManager<Cadence>>(Cadences);
        services.AddSingleton<INamedCatalogManager<LeadStatus>>(LeadStatuses);
        services.AddSingleton<INamedCatalogManager<OpportunityStage>>(OpportunityStages);
        services.AddSingleton<ICatalogManager<OmnichannelActivityBatch>>(Batches);
        services.AddSingleton(ActivityManager.Object);
        services.AddSingleton(DispositionService.Object);
        services.AddSingleton(LoadCoordinator.Object);
        services.AddSingleton(Dialer.Object);

        var dispositionCatalog = new Mock<INamedCatalog<OmnichannelDisposition>>();
        dispositionCatalog.Setup(catalog => catalog.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string id, CancellationToken _) => ValueTask.FromResult(Dispositions.Items.FirstOrDefault(item => item.ItemId == id)));
        dispositionCatalog.Setup(catalog => catalog.GetAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<string> ids, CancellationToken _) => ValueTask.FromResult<IReadOnlyCollection<OmnichannelDisposition>>(
                Dispositions.Items.Where(item => ids.Contains(item.ItemId)).ToList()));
        services.AddSingleton(dispositionCatalog.Object);

        var actionCatalog = new Mock<ISourceCatalog<SubjectAction>>();
        actionCatalog.Setup(catalog => catalog.GetAllAsync(It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult<IReadOnlyCollection<SubjectAction>>(SubjectActions.ToList()));
        services.AddSingleton(actionCatalog.Object);

        var actionManager = new Mock<ISourceCatalogManager<SubjectAction>>();
        actionManager.Setup(manager => manager.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string id, CancellationToken _) => ValueTask.FromResult(SubjectActions.FirstOrDefault(action => action.ItemId == id)));
        actionManager.Setup(manager => manager.DeleteAsync(It.IsAny<SubjectAction>(), It.IsAny<CancellationToken>()))
            .Returns((SubjectAction action, CancellationToken _) => ValueTask.FromResult(SubjectActions.Remove(action)));
        services.AddSingleton(actionManager.Object);

        var contentManager = new Mock<IContentManager>();
        contentManager.Setup(manager => manager.GetAsync(It.IsAny<string>(), It.IsAny<VersionOptions>()))
            .ReturnsAsync((string id, VersionOptions _) => id is not null && Contacts.TryGetValue(id, out var contact) ? contact : null);
        contentManager.Setup(manager => manager.GetAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<VersionOptions>()))
            .ReturnsAsync((IEnumerable<string> ids, VersionOptions _) => ids.Where(Contacts.ContainsKey).Select(id => Contacts[id]).ToList());
        services.AddSingleton(contentManager.Object);

        var clock = new Mock<IClock>();
        clock.Setup(value => value.UtcNow).Returns(Now);
        services.AddSingleton(clock.Object);

        var descriptor = new ShellDescriptor();

        if (CrmEnabled)
        {
            descriptor.Features.Add(new ShellFeature(CrestApps.OrchardCore.Omnichannel.Core.OmnichannelConstants.Features.Crm));
        }

        services.AddSingleton(descriptor);

        return services.BuildServiceProvider();
    }

    public OmnichannelActivity AddActivity(string id, Action<OmnichannelActivity> configure = null)
    {
        var activity = new OmnichannelActivity
        {
            ItemId = id,
            ContactContentItemId = "contact-1",
            ContactContentType = "Contact",
            SubjectContentType = "SalesCall",
            Channel = "Phone",
            Status = ActivityStatus.NotStated,
            ScheduledUtc = Now.AddDays(1),
            AssignedToId = "someone-else",
            AssignedToUsername = "mark",
            PreferredDestination = "+15551234567",
            AISessionId = "session-secret",
            Instructions = "Call before noon.",
        };

        configure?.Invoke(activity);
        Activities[id] = activity;

        return activity;
    }

    public ContentItem AddContact(string id = "contact-1", string displayText = "Jane Doe")
    {
        var contact = new ContentItem
        {
            ContentItemId = id,
            ContentType = "Contact",
            DisplayText = displayText,
            Published = true,
        };

        Contacts[id] = contact;

        return contact;
    }
}

/// <summary>
/// An in-memory catalog manager.
/// </summary>
internal class FakeCatalogManager<T> : ICatalogManager<T>
    where T : CatalogItem, new()
{
    private int _nextId;

    public List<T> Items { get; } = [];

    public List<T> Created { get; } = [];

    public List<T> Updated { get; } = [];

    public List<T> Deleted { get; } = [];

    public Func<T, ValidationResultDetails> Validate { get; set; } = _ => new ValidationResultDetails();

    public T Add(T item)
    {
        Items.Add(item);

        return item;
    }

    public ValueTask<T> FindByIdAsync(string id, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(Items.FirstOrDefault(item => string.Equals(item.ItemId, id, StringComparison.Ordinal)));

    public ValueTask<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromResult<IEnumerable<T>>(Items.ToList());

    public ValueTask<PageResult<T>> PageAsync<TQuery>(int page, int pageSize, TQuery context, CancellationToken cancellationToken = default)
        where TQuery : QueryContext
    {
        var matches = Items
            .Where(item => string.IsNullOrEmpty(context?.Name) || NameOf(item)?.Contains(context.Name, StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        return ValueTask.FromResult(new PageResult<T>
        {
            Count = matches.Count,
            Entries = matches.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
        });
    }

    public ValueTask<bool> DeleteAsync(T model, CancellationToken cancellationToken = default)
    {
        Deleted.Add(model);

        return ValueTask.FromResult(Items.Remove(model));
    }

    public ValueTask<T> NewAsync(JsonNode data = null, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(new T { ItemId = $"new-{++_nextId}" });

    public ValueTask CreateAsync(T model, CancellationToken cancellationToken = default)
    {
        Created.Add(model);
        Items.Add(model);

        return ValueTask.CompletedTask;
    }

    public ValueTask UpdateAsync(T model, JsonNode data = null, CancellationToken cancellationToken = default)
    {
        Updated.Add(model);

        return ValueTask.CompletedTask;
    }

    public ValueTask<ValidationResultDetails> ValidateAsync(T model, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(Validate(model));

    /// <summary>
    /// A rule that fails when the record has no name, as the omnichannel catalog handlers do.
    /// </summary>
    public static ValidationResultDetails RequireName(T model)
    {
        var validation = new ValidationResultDetails();

        if (string.IsNullOrWhiteSpace(NameOf(model)))
        {
            validation.Fail(new ValidationResult("Name is required."));
        }

        return validation;
    }

    private static string NameOf(T item)
        => item switch
        {
            INameAwareModel named => named.Name,
            IDisplayTextAwareModel titled => titled.DisplayText,
            _ => item?.ItemId,
        };
}

/// <summary>
/// An in-memory catalog manager of named records.
/// </summary>
internal sealed class FakeNamedCatalogManager<T> : FakeCatalogManager<T>, INamedCatalogManager<T>
    where T : CatalogItem, INameAwareModel, new()
{
    public ValueTask<T> NewAsync(string name, JsonNode data = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("The tools create named records without a name and set it afterwards, as the editors do.");

    public ValueTask<T> FindByNameAsync(string name, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(Items.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)));
}
