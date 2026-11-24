using System.Security.Claims;
using CrestApps.Core.Security;
using CrestApps.OrchardCore.BusinessHours.Core;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.ServiceLevels;
using CrestApps.OrchardCore.ServiceLevels.Core;
using CrestApps.OrchardCore.ServiceLevels.Core.Indexes;
using CrestApps.OrchardCore.ServiceLevels.Core.Services;
using CrestApps.OrchardCore.ServiceLevels.Models;
using CrestApps.OrchardCore.ServiceLevels.Services;
using CrestApps.OrchardCore.Tests.Agent;
using CrestApps.OrchardCore.Tests.Calendars;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.ServiceLevels.AI;

/// <summary>
/// The service-level AI tools on a real SQLite database: a ticket policy, timers written straight to the timer store, a
/// caller published through <see cref="IUserAccessor"/>, a local clock five hours behind UTC, a ticket access provider
/// that lets the caller see only some tickets, and record titles from a fake record directory.
/// </summary>
internal sealed class ServiceLevelAIToolHarness : IAsyncDisposable
{
    public const string PolicyId = "policy-standard";

    private ServiceLevelAIToolHarness(CalendarTestHarness harness, CallerAccessor caller)
    {
        Harness = harness;
        Caller = caller;
    }

    public CalendarTestHarness Harness { get; }

    public CallerAccessor Caller { get; }

    public static DateTime Now => CalendarTestHarness.Now;

    /// <summary>
    /// Creates the harness.
    /// </summary>
    /// <param name="visibleTickets">The tickets the ticket access provider shows; <see langword="null"/> registers no provider.</param>
    /// <returns>The harness.</returns>
    public static async Task<ServiceLevelAIToolHarness> CreateAsync(string[] visibleTickets = null)
    {
        var caller = new CallerAccessor();
        var records = new Mock<IRecordDirectory>();

        records.Setup(directory => directory.GetSummariesAsync(It.IsAny<IEnumerable<RecordRef>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<RecordRef> subjects, CancellationToken _) => (IReadOnlyDictionary<RecordRef, RecordSummary>)subjects
                .Where(subject => subject.Kind == "Ticket")
                .ToDictionary(subject => subject, subject => new RecordSummary
                {
                    Id = subject.Id,
                    Title = $"T-{subject.Id} Printer jams",
                    AdminUrl = $"/Admin/tickets/{subject.Id}",
                }));

        var localClock = new Mock<ILocalClock>();

        localClock.Setup(clock => clock.ConvertToLocalAsync(It.IsAny<DateTimeOffset>()))
            .ReturnsAsync((DateTimeOffset value) => value.ToOffset(TimeSpan.FromHours(-5)));

        var harness = await CalendarTestHarness.CreateAsync(
            services =>
            {
                services.AddOpenHours();
                services.AddServiceLevelsCore();
                services.AddScoped<ServiceLevelDueTimers>();
                services.AddSingleton<IUserAccessor>(caller);
                services.AddSingleton(localClock.Object);
                services.AddSingleton(records.Object);
                services.AddSingleton<IServiceLevelScopeProvider>(new TicketScope());

                if (visibleTickets is not null)
                {
                    services.AddSingleton<IServiceLevelTimerAccessProvider>(new TicketAccess(visibleTickets));
                }
            },
            ServiceLevelsIndexSchema.CreateAsync,
            [new ServiceLevelPolicyIndexProvider(), new ServiceLevelTimerIndexProvider()],
            [ServiceLevelsConstants.CollectionName]);

        await harness.RunAsync(async services =>
        {
            var policies = services.GetRequiredService<IServiceLevelPolicyManager>();
            var policy = await policies.NewAsync();

            policy.ItemId = PolicyId;
            policy.Name = "Standard";
            policy.Scope = "Tickets";
            policy.Targets = [new ServiceLevelTarget { Metric = ServiceLevelMetrics.FirstResponse, Minutes = 60 }];
            await policies.CreateAsync(policy);
        });

        return new ServiceLevelAIToolHarness(harness, caller);
    }

    /// <summary>
    /// Writes timers to the store.
    /// </summary>
    /// <param name="timers">The timers.</param>
    /// <returns>A task that completes when they are saved.</returns>
    public Task AddAsync(params ServiceLevelTimer[] timers)
        => Harness.RunAsync(async services =>
        {
            var store = services.GetRequiredService<IServiceLevelTimerStore>();

            foreach (var timer in timers)
            {
                await store.CreateAsync(timer);
            }
        });

    /// <summary>
    /// A running timer of a record, due some minutes from now (negative for past due), that warns 20 minutes before.
    /// </summary>
    public static ServiceLevelTimer NewTimer(string id, string kind, string recordId, int dueInMinutes, ServiceLevelTimerState state = ServiceLevelTimerState.Running, string metric = ServiceLevelMetrics.FirstResponse)
        => new()
        {
            ItemId = id,
            Subject = new RecordRef(kind, recordId),
            Metric = metric,
            PolicyId = PolicyId,
            Priority = "urgent",
            TargetSeconds = 3600,
            State = state,
            StartedUtc = Now.AddMinutes(dueInMinutes - 60),
            DueUtc = Now.AddMinutes(dueInMinutes),
            WarnAtUtc = Now.AddMinutes(dueInMinutes - 20),
            CountingSinceUtc = Now.AddMinutes(dueInMinutes - 60),
            BreachedUtc = dueInMinutes <= 0 && state == ServiceLevelTimerState.Running ? Now.AddMinutes(dueInMinutes) : null,
        };

    /// <summary>
    /// Calls a tool as the current caller.
    /// </summary>
    public Task<string> InvokeAsync(AIFunction tool, Dictionary<string, object> arguments = null)
        => Harness.RunAsync(services => AgentToolTestContext.InvokeAsync(tool, services, arguments), commit: false);

    public ValueTask DisposeAsync()
        => Harness.DisposeAsync();

    /// <summary>
    /// The caller the chat hub would publish.
    /// </summary>
    internal sealed class CallerAccessor : IUserAccessor
    {
        public ClaimsPrincipal User { get; set; }

        public void SignIn(params string[] permissions)
            => User = CalendarTestHarness.User("ana", permissions);
    }

    private sealed class TicketAccess : IServiceLevelTimerAccessProvider
    {
        private readonly HashSet<string> _visible;

        public TicketAccess(IEnumerable<string> visible)
        {
            _visible = new HashSet<string>(visible, StringComparer.Ordinal);
        }

        public string SubjectKind => "Ticket";

        public Task<IReadOnlySet<string>> GetVisibleAsync(ClaimsPrincipal user, IReadOnlyCollection<string> subjectIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlySet<string>>(subjectIds.Where(_visible.Contains).ToHashSet(StringComparer.Ordinal));
    }

    private sealed class TicketScope : IServiceLevelScopeProvider
    {
        public string Name => "Tickets";

        public LocalizationSource DisplayName { get; } = LocalizationSource.Create<TicketScope>("Tickets");

        public Task<IReadOnlyList<ServiceLevelOption>> GetMetricsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ServiceLevelOption>>([new(ServiceLevelMetrics.FirstResponse, "First response"), new(ServiceLevelMetrics.Resolution, "Resolution")]);

        public Task<IReadOnlyList<ServiceLevelOption>> GetPrioritiesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ServiceLevelOption>>([new("urgent", "Urgent")]);
    }
}
