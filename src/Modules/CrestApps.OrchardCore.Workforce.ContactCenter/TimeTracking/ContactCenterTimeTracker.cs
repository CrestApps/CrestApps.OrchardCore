using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.IntervalStatistics;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.TimeTracker;
using CrestApps.OrchardCore.TimeTracker.Models;
using CrestApps.OrchardCore.TimeTracker.Services;
using CrestApps.OrchardCore.Workforce.Core.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Entities;
using OrchardCore.Settings;
using IClock = OrchardCore.Modules.IClock;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.TimeTracking;

/// <summary>
/// The Time Tracker stage of each agent state and break reason, kept in the site settings.
/// </summary>
public sealed class ContactCenterTimeStages
{
    /// <summary>
    /// Gets or sets the stage of each agent state, by the state's name; a state with no stage isn't tracked.
    /// </summary>
    public Dictionary<string, string> States { get; set; } = [];

    /// <summary>
    /// Gets or sets the stage of a state's reason, by the reason's name, such as Lunch for a break; it comes before
    /// the state's stage.
    /// </summary>
    public Dictionary<string, string> Reasons { get; set; } = [];
}

/// <summary>
/// Keeps an agent's states in Time Tracker: the last minutes of the agent state log, each state (or its reason) in its
/// stage. The states are kept over the time clock's work, which is cut around them.
/// </summary>
public sealed class ContactCenterTimeTrackerSync
{
    private static readonly TimeSpan _openTolerance = TimeSpan.FromSeconds(1);

    private readonly IAgentStateHistory _history;
    private readonly ITimeTrackerService _tracker;
    private readonly ISiteService _siteService;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterTimeTrackerSync"/> class.
    /// </summary>
    public ContactCenterTimeTrackerSync(IAgentStateHistory history, ITimeTrackerService tracker, ISiteService siteService, IClock clock)
    {
        _history = history;
        _tracker = tracker;
        _siteService = siteService;
        _clock = clock;
    }

    /// <summary>
    /// Replaces an agent's Time Tracker entries from a time to now with their states.
    /// </summary>
    /// <param name="agentId">The agent profile.</param>
    /// <param name="userId">The agent's user.</param>
    /// <param name="userName">The agent's user name.</param>
    /// <param name="fromUtc">Where the log is read from.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    public async Task<TimeTrackerResult> SyncAsync(string agentId, string userId, string userName, DateTime fromUtc, CancellationToken cancellationToken = default)
    {
        var stages = (await _siteService.GetSiteSettingsAsync()).GetOrCreate<ContactCenterTimeStages>();
        var now = _clock.UtcNow;

        return await _tracker.SyncAsync(new TimeEntrySync
        {
            UserId = userId,
            UserName = userName,
            ManagedBy = WorkforceContactCenterConstants.TimeTrackerClockId,
            Source = TimeTrackerConstants.Sources.ContactCenter,
            FromUtc = fromUtc,
            ToUtc = now.AddMinutes(1),
            ReplacesRangeOnly = true,
            Overrides = [TimeClockTimeTracker.ClockId],
            BuildAsync = async token => Build(await _history.GetSpansAsync([agentId], fromUtc, now, token), stages, now),
        }, cancellationToken);
    }

    /// <summary>
    /// Turns state spans into entries: back-to-back spans in one stage make one entry, a state with no stage leaves a
    /// gap, and the state the agent is in now stays open.
    /// </summary>
    internal static List<TimeEntry> Build(IEnumerable<AgentStateSpan> spans, ContactCenterTimeStages stages, DateTime nowUtc)
    {
        var ordered = spans.OrderBy(span => span.StartUtc).ToList();
        var entries = new List<TimeEntry>();

        for (var i = 0; i < ordered.Count; i++)
        {
            var span = ordered[i];
            var stageId = StageOf(span, stages);

            if (string.IsNullOrEmpty(stageId) || span.EndUtc <= span.StartUtc)
            {
                continue;
            }

            var isNow = i == ordered.Count - 1 && span.Status != AgentPresenceStatus.Offline && span.EndUtc >= nowUtc - _openTolerance;
            var note = string.IsNullOrEmpty(span.Reason) ? null : span.Reason;

            if (entries.Count > 0 && entries[^1] is { } last && last.EndUtc == span.StartUtc && last.StageId == stageId && last.Note == note)
            {
                last.EndUtc = isNow ? null : span.EndUtc;

                continue;
            }

            entries.Add(new TimeEntry
            {
                StageId = stageId,
                StartUtc = span.StartUtc,
                EndUtc = isNow ? null : span.EndUtc,
                Source = TimeTrackerConstants.Sources.ContactCenter,
                Note = note,
            });
        }

        return entries;
    }

    private static string StageOf(AgentStateSpan span, ContactCenterTimeStages stages)
    {
        if (!string.IsNullOrEmpty(span.Reason) && stages.Reasons.TryGetValue(span.Reason, out var byReason) && !string.IsNullOrEmpty(byReason))
        {
            return byReason;
        }

        return stages.States.GetValueOrDefault(span.Status.ToString());
    }
}

/// <summary>
/// Keeps an agent's states in Time Tracker after each change, reading the log from a few minutes before it. The
/// sync rebuilds from the log, so an event handled twice changes nothing.
/// </summary>
public sealed class AgentStateTimeTrackerHandler : IContactCenterEventHandler
{
    private static readonly TimeSpan _lookBack = TimeSpan.FromMinutes(10);

    private readonly ContactCenterTimeTrackerSync _sync;
    private readonly IAgentProfileManager _agents;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentStateTimeTrackerHandler"/> class.
    /// </summary>
    public AgentStateTimeTrackerHandler(ContactCenterTimeTrackerSync sync, IAgentProfileManager agents)
    {
        _sync = sync;
        _agents = agents;
    }

    /// <inheritdoc/>
    public string HandlerId => "Workforce/ContactCenterTimeTracker/v1";

    /// <inheritdoc/>
    public ContactCenterHandlerReplaySafety ReplaySafety => ContactCenterHandlerReplaySafety.NaturallyIdempotent;

    /// <inheritdoc/>
    public async Task HandleAsync(InteractionEvent interactionEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(interactionEvent);

        if (interactionEvent.EventType != ContactCenterConstants.Events.AgentStateChanged)
        {
            return;
        }

        var data = interactionEvent.GetData<AgentStateChangedEventData>();

        if (data is null || string.IsNullOrEmpty(data.UserId) || string.IsNullOrEmpty(data.AgentId))
        {
            return;
        }

        var agent = await _agents.FindByIdAsync(data.AgentId, cancellationToken);
        var at = data.ChangedUtc == default ? interactionEvent.OccurredUtc : data.ChangedUtc;

        await _sync.SyncAsync(data.AgentId, data.UserId, agent?.UserName ?? agent?.DisplayName, at - _lookBack, cancellationToken);
    }
}

/// <summary>
/// Names the contact center as the clock that keeps agents' state entries, so Time Tracker shows them as kept there.
/// </summary>
public sealed class ContactCenterExternalClock : IExternalTimeClock
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterExternalClock"/> class.
    /// </summary>
    public ContactCenterExternalClock(IStringLocalizer<ContactCenterExternalClock> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public Task<ExternalTimeClockInfo> FindAsync(string userId, CancellationToken cancellationToken = default)
        => Task.FromResult<ExternalTimeClockInfo>(null);

    /// <inheritdoc/>
    public Task<ExternalTimeClockInfo> FindByIdAsync(string id, CancellationToken cancellationToken = default)
        => Task.FromResult(id == WorkforceContactCenterConstants.TimeTrackerClockId
            ? new ExternalTimeClockInfo { Id = id, Name = S["the Contact Center's agent states"] }
            : null);
}

/// <summary>
/// Gives the usual agent states a stage: one with the stage's name, or a new one. Work states count toward Paid and
/// Worked; breaks and time away count toward nothing until a site says how.
/// </summary>
public sealed class ContactCenterTimeStageSeeder
{
    private readonly ITimeStageManager _stages;
    private readonly ITimeCategoryManager _categories;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterTimeStageSeeder"/> class.
    /// </summary>
    public ContactCenterTimeStageSeeder(ITimeStageManager stages, ITimeCategoryManager categories)
    {
        _stages = stages;
        _categories = categories;
    }

    /// <summary>
    /// Fills in the stages of the usual states that have none, keeping the ones set.
    /// </summary>
    /// <param name="settings">The settings to fill in.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The settings.</returns>
    public async Task<ContactCenterTimeStages> BuildAsync(ContactCenterTimeStages settings, CancellationToken cancellationToken = default)
    {
        settings ??= new ContactCenterTimeStages();

        var stages = (await _stages.GetAllAsync(cancellationToken)).ToList();

        foreach (var (state, name, color, worked) in Defaults())
        {
            if (settings.States.TryGetValue(state.ToString(), out var set) && !string.IsNullOrEmpty(set))
            {
                continue;
            }

            var stage = stages.FirstOrDefault(stage => string.Equals(stage.Name, name, StringComparison.OrdinalIgnoreCase));

            if (stage is null)
            {
                stage = await _stages.NewAsync(cancellationToken: cancellationToken);
                stage.Name = name;
                stage.Color = color;
                stage.Position = 100 + (stages.Count * 10);
                stage.SelfService = false;

                if (worked)
                {
                    stage.CategoryIds.Add((await CategoryAsync("Paid", "PAID", "#2b8a3e", 10, cancellationToken)).ItemId);
                    stage.CategoryIds.Add((await CategoryAsync("Worked", "WORKED", "#1971c2", 20, cancellationToken)).ItemId);
                }

                await _stages.CreateAsync(stage, cancellationToken: cancellationToken);
                stages.Add(stage);
            }

            settings.States[state.ToString()] = stage.ItemId;
        }

        return settings;
    }

    // Offered work and asking for a break are still available; do not disturb, after hours and offline aren't tracked.
    private static IEnumerable<(AgentPresenceStatus State, string Name, string Color, bool Worked)> Defaults()
    {
        yield return (AgentPresenceStatus.Available, "Available", "#2b8a3e", true);
        yield return (AgentPresenceStatus.Reserved, "Available", "#2b8a3e", true);
        yield return (AgentPresenceStatus.RequestBreak, "Available", "#2b8a3e", true);
        yield return (AgentPresenceStatus.Busy, "On a call", "#1971c2", true);
        yield return (AgentPresenceStatus.WrapUp, "Wrap-up", "#6741d9", true);
        yield return (AgentPresenceStatus.Break, "Break", "#f59f00", false);
        yield return (AgentPresenceStatus.Away, "Away", "#868e96", false);
        yield return (AgentPresenceStatus.Meeting, "Meeting", "#6741d9", true);
        yield return (AgentPresenceStatus.Training, "Training", "#1971c2", true);
    }

    private async Task<TimeCategory> CategoryAsync(string name, string code, string color, int position, CancellationToken cancellationToken)
    {
        var found = (await _categories.GetAllAsync(cancellationToken)).FirstOrDefault(category => string.Equals(category.Name, name, StringComparison.OrdinalIgnoreCase) || string.Equals(category.Code, code, StringComparison.OrdinalIgnoreCase));

        if (found is not null)
        {
            return found;
        }

        var category = await _categories.NewAsync(cancellationToken: cancellationToken);

        category.Name = name;
        category.Code = code;
        category.Color = color;
        category.Position = position;
        await _categories.CreateAsync(category, cancellationToken: cancellationToken);

        return category;
    }
}
