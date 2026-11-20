using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using CrestApps.Core;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.BusinessHours.Core;
using CrestApps.OrchardCore.BusinessHours.Models;
using CrestApps.OrchardCore.Core;
using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Parties.Services;
using CrestApps.OrchardCore.ServiceLevels;
using CrestApps.OrchardCore.ServiceLevels.Core;
using CrestApps.OrchardCore.ServiceLevels.Core.Handlers;
using CrestApps.OrchardCore.ServiceLevels.Core.Indexes;
using CrestApps.OrchardCore.ServiceLevels.Models;
using CrestApps.OrchardCore.ServiceLevels.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tickets;
using CrestApps.OrchardCore.Tickets.Core.Indexes;
using CrestApps.OrchardCore.Tickets.Core.Services;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using CrestApps.OrchardCore.Timeline;
using CrestApps.OrchardCore.Timeline.Core;
using CrestApps.OrchardCore.Timeline.Core.Indexes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Documents;
using OrchardCore.Localization;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Tickets;

/// <summary>
/// Tickets on a real SQLite store with the real ticket service, service-level tracker, timeline and number sequence, a
/// lock that really blocks, and content items saved through the session. Each <see cref="RunAsync{T}"/> is one unit of
/// work in its own scope, as a request is.
/// </summary>
internal sealed class TicketKit : IAsyncDisposable
{
    public const string PolicyId = "4tkpolicy0000000000000000a";

    public static readonly TicketStatus New = new() { ItemId = "new", Name = "New", Category = TicketStatusCategory.New, Order = 10 };
    public static readonly TicketStatus Open = new() { ItemId = "open", Name = "Open", Category = TicketStatusCategory.Open, Order = 20 };
    public static readonly TicketStatus Pending = new() { ItemId = "pending", Name = "Waiting", Category = TicketStatusCategory.Pending, PausesTimers = true, Order = 30 };
    public static readonly TicketStatus Solved = new() { ItemId = "solved", Name = "Solved", Category = TicketStatusCategory.Solved, Order = 50 };
    public static readonly TicketStatus Closed = new() { ItemId = "closed", Name = "Closed", Category = TicketStatusCategory.Closed, Order = 60 };
    public static readonly TicketPriority Normal = new() { ItemId = "normal", Name = "Normal", Rank = 2 };
    public static readonly TicketPriority Urgent = new() { ItemId = "urgent", Name = "Urgent", Rank = 4 };

    private TicketKit(CalendarTestHarness harness, KitObservers observers, KitSender sender)
    {
        Harness = harness;
        Observers = observers;
        Sender = sender;
    }

    /// <summary>
    /// Gets the e-mail sender the outbox uses.
    /// </summary>
    public KitSender Sender { get; }

    public CalendarTestHarness Harness { get; }

    public KitObservers Observers { get; }

    public static async Task<TicketKit> CreateAsync(bool withPolicy = false, Action<TicketDesk> configureDesk = null, Action<IServiceCollection> configureServices = null)
    {
        var observers = new KitObservers();
        var sender = new KitSender();
        var desk = new TicketDesk { ItemId = "support", Name = "Support", ServiceLevelPolicyId = withPolicy ? PolicyId : null, ReopenWindowDays = 7 };
        configureDesk?.Invoke(desk);

        var harness = await CalendarTestHarness.CreateAsync(
            services =>
            {
                services.AddCatalogs().AddCatalogManagers();
                services.AddSingleton<CommittedDocuments>();
                services.AddScoped<IDocumentManager<DictionaryDocument<BusinessHoursSchedule>>, CommittingDocumentManager<BusinessHoursSchedule>>();
                services.AddBusinessHoursSchedules();
                services.AddServiceLevelsCore();
                services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<ServiceLevelPolicy>, ServiceLevelPolicyHandler>());
                services.AddSingleton<IServiceLevelScopeProvider>(new TicketsScope());
                services.AddTimelineCore();
                services.AddNumberSequence();
                services.AddSingleton<IDistributedLock, BlockingLock>();
                services.AddSingleton(Catalog(New, Open, Pending, Solved, Closed));
                services.AddSingleton(Catalog(Normal, Urgent));
                services.AddSingleton(Catalog(desk));
                services.AddSingleton(Definitions());
                services.AddSingleton(new Mock<IPartyDirectory>().Object);
                services.AddScoped(provider => ContentManagerOver(provider.GetRequiredService<ISession>()));
                services.AddSingleton(observers);
                services.AddScoped<ITicketObserver>(provider => new KitObserver(provider.GetRequiredService<KitObservers>(), provider));
                services.AddScoped<ITicketService, DefaultTicketService>();
                services.AddSingleton<global::CrestApps.OrchardCore.Emails.ITemplatedEmailSender>(sender);
                services.AddScoped<TicketEmailOutbox>();
                configureServices?.Invoke(services);
            },
            async schema =>
            {
                await TicketIndexSchema.CreateAsync(schema);
                await TicketIndexSchema.CreateServiceDeskAsync(schema);
                await TimelineIndexSchema.CreateAsync(schema);
                await ServiceLevelsIndexSchema.CreateAsync(schema);
            },
            [new TicketIndexProvider(), new TicketTimeElapsedMarkIndexProvider(), new TicketEmailOutboxIndexProvider(), new TicketProtectedItemIndexProvider(), new TicketLinkIndexProvider(), new TimelineEntryIndexProvider(), new ServiceLevelPolicyIndexProvider(), new ServiceLevelTimerIndexProvider()],
            [TimelineConstants.CollectionName, ServiceLevelsConstants.CollectionName, TicketsConstants.CollectionName]);

        if (withPolicy)
        {
            await harness.RunAsync(async services =>
            {
                var policies = services.GetRequiredService<IServiceLevelPolicyManager>();
                var policy = await policies.NewAsync();
                policy.ItemId = PolicyId;
                policy.Name = "Standard";
                policy.Scope = "Tickets";
                policy.Targets =
                [
                    new ServiceLevelTarget { Metric = ServiceLevelMetrics.FirstResponse, Minutes = 60 },
                    new ServiceLevelTarget { Metric = ServiceLevelMetrics.NextResponse, Minutes = 60 },
                    new ServiceLevelTarget { Metric = ServiceLevelMetrics.Resolution, Minutes = 480 },
                ];
                await policies.CreateAsync(policy);
            });
        }

        return new TicketKit(harness, observers, sender);
    }

    public Task<T> RunAsync<T>(Func<IServiceProvider, Task<T>> work)
        => Harness.RunAsync(work);

    public Task<T> ReadAsync<T>(Func<IServiceProvider, Task<T>> work)
        => Harness.RunAsync(work, commit: false);

    public Task<TicketResult> CreateAsync(string subject = "Printer jams", string body = "It jams on every page.")
        => RunAsync(services => services.GetRequiredService<ITicketService>().CreateAsync(new TicketCreateRequest
        {
            Subject = subject,
            Body = body,
            DeskId = "support",
            Channel = TicketsConstants.Channels.Agent,
        }, TestContext.Current.CancellationToken));

    public Task<ContentItem> GetAsync(string ticketId)
        => ReadAsync(services => services.GetRequiredService<ITicketService>().GetAsync(ticketId));

    public Task<IReadOnlyList<ServiceLevelTimer>> TimersAsync(string ticketId)
        => ReadAsync(services => services.GetRequiredService<IServiceLevelTracker>().GetTimersAsync(DefaultTicketService.SubjectOf(ticketId), cancellationToken: TestContext.Current.CancellationToken));

    public Task<IReadOnlyList<global::CrestApps.OrchardCore.Timeline.Models.TimelineEntry>> EntriesAsync(string ticketId)
        => ReadAsync(async services => (await services.GetRequiredService<global::CrestApps.OrchardCore.Timeline.Services.ITimelineService>().ListAsync(new global::CrestApps.OrchardCore.Timeline.Models.TimelineQuery
        {
            Subject = DefaultTicketService.SubjectOf(ticketId),
            IncludeInternal = true,
            Take = 100,
        }, TestContext.Current.CancellationToken)).Entries);

    public ValueTask DisposeAsync()
        => Harness.DisposeAsync();

    private static ICatalog<T> Catalog<T>(params T[] records)
        where T : CrestApps.Core.Models.CatalogItem
    {
        var catalog = new Mock<ICatalog<T>>();
        catalog.Setup(value => value.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(records);
        catalog.Setup(value => value.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, CancellationToken _) => records.FirstOrDefault(record => record.ItemId == id));

        return catalog.Object;
    }

    private static IContentDefinitionManager Definitions()
    {
        var ticket = new ContentTypeDefinition(
            TicketsConstants.DefaultContentType,
            TicketsConstants.DefaultContentType,
            [new ContentTypePartDefinition(TicketsConstants.PartName, new ContentPartDefinition(TicketsConstants.PartName, [], []), [])],
            []);
        var definitions = new Mock<IContentDefinitionManager>();
        definitions.Setup(manager => manager.GetTypeDefinitionAsync(It.IsAny<string>()))
            .ReturnsAsync((string name) => name == ticket.Name ? ticket : null);

        return definitions.Object;
    }

    /// <summary>
    /// What the observers were told, from every scope.
    /// </summary>
    internal sealed class KitObservers
    {
        public ConcurrentQueue<string> Events { get; } = new();

        /// <summary>
        /// Gets or sets what an observer does when told, in the scope that raised the event, as a workflow does.
        /// </summary>
        public Func<IServiceProvider, string, ContentItem, Task> React { get; set; }
    }

    /// <summary>
    /// An e-mail sender that records what it sent, and fails or throws when a test says so.
    /// </summary>
    internal sealed class KitSender : global::CrestApps.OrchardCore.Emails.ITemplatedEmailSender
    {
        public ConcurrentQueue<(global::CrestApps.OrchardCore.Emails.TemplatedEmail Email, global::CrestApps.OrchardCore.Emails.TemplatedEmailContent Content)> Sent { get; } = new();

        /// <summary>
        /// Gets or sets what a send does: <see langword="null"/> sends, otherwise it is thrown.
        /// </summary>
        public Exception Failure { get; set; }

        public bool CanSend => true;

        public async Task<bool> SendAsync(global::CrestApps.OrchardCore.Emails.TemplatedEmail email, Func<CancellationToken, Task<global::CrestApps.OrchardCore.Emails.TemplatedEmailContent>> build, CancellationToken cancellationToken = default)
        {
            if (Failure is not null)
            {
                throw Failure;
            }

            Sent.Enqueue((email, await build(cancellationToken)));

            return true;
        }
    }

    private sealed class KitObserver : ITicketObserver
    {
        private readonly KitObservers _observers;
        private readonly IServiceProvider _services;

        public KitObserver(KitObservers observers, IServiceProvider services)
        {
            _observers = observers;
            _services = services;
        }

        public Task CreatedAsync(ContentItem ticket, CancellationToken cancellationToken = default) => TellAsync("Created", ticket);

        public Task UpdatedAsync(ContentItem ticket, IReadOnlyList<global::CrestApps.OrchardCore.Timeline.Models.TimelineFieldChange> changes, CancellationToken cancellationToken = default)
            => TellAsync("Updated:" + string.Join(",", changes.Select(change => change.Key)), ticket);

        public Task RepliedAsync(ContentItem ticket, global::CrestApps.OrchardCore.Timeline.Models.TimelineEntry entry, bool fromRequester, CancellationToken cancellationToken = default)
            => TellAsync(fromRequester ? "CustomerReplied" : entry.Visibility == global::CrestApps.OrchardCore.Timeline.Models.TimelineVisibility.Internal ? "NoteAdded" : "AgentReplied", ticket);

        public Task StatusChangedAsync(ContentItem ticket, TicketStatus previous, TicketStatus current, CancellationToken cancellationToken = default)
            => TellAsync("StatusChanged:" + current?.ItemId, ticket);

        public Task AssignedAsync(ContentItem ticket, string previousAssigneeId, CancellationToken cancellationToken = default) => TellAsync("Assigned", ticket);

        public Task MergedAsync(ContentItem ticket, ContentItem into, CancellationToken cancellationToken = default) => TellAsync("Merged", ticket);

        public Task RatedAsync(ContentItem ticket, TicketSatisfaction satisfaction, CancellationToken cancellationToken = default) => TellAsync("Rated:" + satisfaction?.Score, ticket);

        public Task ApprovalRequestedAsync(ContentItem ticket, TicketApprovalStep step, CancellationToken cancellationToken = default) => TellAsync("ApprovalRequested:" + step.Name, ticket);

        private Task TellAsync(string name, ContentItem ticket)
        {
            _observers.Events.Enqueue(name);

            return _observers.React?.Invoke(_services, name, ticket) ?? Task.CompletedTask;
        }
    }

    /// <summary>
    /// A distributed lock that blocks like the real one: one holder per key, no re-entry, a waiter gives up after the
    /// timeout.
    /// </summary>
    private sealed class BlockingLock : IDistributedLock
    {
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _keys = new(StringComparer.Ordinal);

        public async Task<ILocker> AcquireLockAsync(string key, TimeSpan? expiration = null)
        {
            var semaphore = _keys.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync();

            return new Locker(semaphore);
        }

        public async Task<(ILocker locker, bool locked)> TryAcquireLockAsync(string key, TimeSpan timeout, TimeSpan? expiration = null)
        {
            var semaphore = _keys.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

            return await semaphore.WaitAsync(timeout) ? (new Locker(semaphore), true) : (null, false);
        }

        public Task<bool> IsLockAcquiredAsync(string key)
            => Task.FromResult(_keys.TryGetValue(key, out var semaphore) && semaphore.CurrentCount == 0);

        private sealed class Locker : ILocker
        {
            private SemaphoreSlim _semaphore;

            public Locker(SemaphoreSlim semaphore)
            {
                _semaphore = semaphore;
            }

            public void Dispose()
                => Interlocked.Exchange(ref _semaphore, null)?.Release();

            public ValueTask DisposeAsync()
            {
                Dispose();

                return ValueTask.CompletedTask;
            }
        }
    }

    // Content items saved straight through the session and found through the ticket index, as the content manager does
    // for a type that isn't versioned.
    private static IContentManager ContentManagerOver(ISession session)
    {
        var contents = new Mock<IContentManager>();
        contents.Setup(manager => manager.NewAsync(It.IsAny<string>()))
            .ReturnsAsync((string type) => new ContentItem { ContentType = type, ContentItemId = CrestApps.Core.UniqueId.GenerateId(), Latest = true, Published = true });
        contents.Setup(manager => manager.CreateAsync(It.IsAny<ContentItem>(), It.IsAny<VersionOptions>()))
            .Returns(async (ContentItem item, VersionOptions _) =>
            {
                item.CreatedUtc ??= CalendarTestHarness.Now;
                item.ModifiedUtc = CalendarTestHarness.Now;
                await session.SaveAsync(item);

                return true;
            });
        contents.Setup(manager => manager.UpdateAsync(It.IsAny<ContentItem>()))
            .Returns(async (ContentItem item) =>
            {
                item.ModifiedUtc = CalendarTestHarness.Now;
                await session.SaveAsync(item);

                return true;
            });
        contents.Setup(manager => manager.GetAsync(It.IsAny<string>(), It.IsAny<VersionOptions>()))
            .Returns(async (string id, VersionOptions _) => await session.Query<ContentItem, TicketIndex>(index => index.ContentItemId == id).FirstOrDefaultAsync()

                // Another content item (such as an account) has no ticket index row.
                ?? (await session.Query<ContentItem>().ListAsync()).FirstOrDefault(item => item.ContentItemId == id));
        contents.Setup(manager => manager.RemoveAsync(It.IsAny<ContentItem>()))
            .Returns((ContentItem item) =>
            {
                session.Delete(item);

                return Task.FromResult(true);
            });

        return contents.Object;
    }

    private sealed class TicketsScope : IServiceLevelScopeProvider
    {
        public string Name => "Tickets";

        [JsonIgnore]
        public LocalizationSource DisplayName { get; } = LocalizationSource.Create<TicketsScope>("Tickets");

        public Task<IReadOnlyList<ServiceLevelOption>> GetMetricsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ServiceLevelOption>>([]);

        public Task<IReadOnlyList<ServiceLevelOption>> GetPrioritiesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ServiceLevelOption>>([]);
    }
}
