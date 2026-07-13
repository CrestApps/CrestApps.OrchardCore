using System.Globalization;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using OrchardCore.Security.Permissions;
using OrchardCore.Users;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Reports;

internal sealed class EnterpriseActivityReportProvider : IReport
{
    private readonly ISession _session;
    private readonly UserManager<IUser> _userManager;
    private readonly EnterpriseActivityReportDefinition _definition;
    private readonly IStringLocalizer _stringLocalizer;

    public EnterpriseActivityReportProvider(
        ISession session,
        UserManager<IUser> userManager,
        EnterpriseActivityReportDefinition definition,
        IStringLocalizer stringLocalizer)
    {
        _session = session;
        _userManager = userManager;
        _definition = definition;
        _stringLocalizer = stringLocalizer;
    }

    public string Name => _definition.Name;

    public LocalizedString DisplayName => _definition.DisplayName();

    public LocalizedString Description => _definition.Description();

    public string Category => _definition.Category;

    public Permission Permission => OmnichannelConstants.Permissions.ViewReports;

    public async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        var fromUtc = _definition.Kind is EnterpriseActivityReportKind.Backlog or EnterpriseActivityReportKind.Aging
            ? DateTime.MinValue
            : context.FromUtc;

        var activities = (await _session.QueryIndex<OmnichannelActivityIndex>(
            index => index.CreatedUtc >= fromUtc && index.CreatedUtc <= context.ToUtc,
            collection: OmnichannelConstants.CollectionName)
            .ListAsync(cancellationToken))
            .ToArray();
        var filteredActivities = OmnichannelReportQuery.Filter(
            activities,
            OmnichannelReportFilter.GetCriteria(context.Filter));
        var users = await ResolveUsersAsync(filteredActivities, cancellationToken);

        return _definition.Kind switch
        {
            EnterpriseActivityReportKind.Backlog => BuildBacklog(filteredActivities, context.ToUtc),
            EnterpriseActivityReportKind.Aging => BuildAging(filteredActivities, context.ToUtc),
            EnterpriseActivityReportKind.SourcePerformance => BuildProgress(filteredActivities, S["Source"].Value, activity => Display(activity.Source)),
            EnterpriseActivityReportKind.ChannelPerformance => BuildProgress(filteredActivities, S["Channel"].Value, activity => Display(activity.Channel)),
            EnterpriseActivityReportKind.KindPerformance => BuildProgress(filteredActivities, S["Activity kind"].Value, activity => activity.Kind.ToString()),
            EnterpriseActivityReportKind.AssignmentPerformance => BuildProgress(filteredActivities, S["Assignment status"].Value, activity => activity.AssignmentStatus.ToString()),
            EnterpriseActivityReportKind.AttemptAnalysis => BuildAttempts(filteredActivities),
            EnterpriseActivityReportKind.ContactTypeWorkload => BuildProgress(filteredActivities, S["Contact type"].Value, activity => Display(activity.ContactContentType)),
            EnterpriseActivityReportKind.UrgencyPerformance => BuildProgress(filteredActivities, S["Urgency"].Value, activity => activity.UrgencyLevel.ToString()),
            EnterpriseActivityReportKind.AssignedUserPerformance => BuildProgress(filteredActivities, S["Assigned user"].Value, activity => ResolveUser(activity.AssignedToId, users)),
            EnterpriseActivityReportKind.CreatedByPerformance => BuildProgress(filteredActivities, S["Created by"].Value, activity => ResolveUser(activity.CreatedById, users)),
            EnterpriseActivityReportKind.UserCompletionTime => BuildCompletionDuration(filteredActivities, S["Assigned user"].Value, activity => ResolveUser(activity.AssignedToId, users)),
            EnterpriseActivityReportKind.UserDailyProductivity => BuildDailyUserProductivity(filteredActivities, users),
            EnterpriseActivityReportKind.CampaignSourceMix => BuildCampaignDimension(filteredActivities, S["Source"].Value, activity => Display(activity.Source)),
            EnterpriseActivityReportKind.CampaignChannelMix => BuildCampaignDimension(filteredActivities, S["Channel"].Value, activity => Display(activity.Channel)),
            EnterpriseActivityReportKind.CampaignDispositionMix => BuildCampaignDimension(filteredActivities, S["Disposition"].Value, activity => Display(activity.DispositionId)),
            EnterpriseActivityReportKind.CampaignAttemptPerformance => BuildCampaignDimension(filteredActivities, S["Attempts"].Value, activity => Math.Max(0, activity.Attempts).ToString(CultureInfo.InvariantCulture)),
            EnterpriseActivityReportKind.OverdueByUser => BuildOverdueByDimension(filteredActivities, context.ToUtc, S["Assigned user"].Value, activity => ResolveUser(activity.AssignedToId, users)),
            EnterpriseActivityReportKind.ChannelEndpointUsage => BuildProgress(filteredActivities, S["Channel endpoint"].Value, activity => Display(activity.ChannelEndpointId)),
            EnterpriseActivityReportKind.CustomerWorkload => BuildProgress(filteredActivities, S["Customer"].Value, activity => Display(activity.ContactContentItemId)),
            EnterpriseActivityReportKind.ScheduleCompletion => BuildScheduleCompletion(filteredActivities),
            _ => new ReportDocument(),
        };
    }

    private IStringLocalizer S => _stringLocalizer;

    private ReportDocument BuildBacklog(IReadOnlyList<OmnichannelActivityIndex> activities, DateTime asOfUtc)
    {
        var open = activities.Where(activity => !IsTerminal(activity.Status)).ToArray();

        return new ReportDocument()
            .Add(ReportSection.ForMetrics(S["Open activity inventory"].Value,
            [
                new ReportMetric(S["Open"].Value, ReportFormat.Number(open.LongLength)),
                new ReportMetric(S["Unassigned"].Value, ReportFormat.Number(open.LongCount(activity => string.IsNullOrEmpty(activity.AssignedToId)))),
                new ReportMetric(S["Overdue"].Value, ReportFormat.Number(open.LongCount(activity => IsOverdue(activity, asOfUtc)))),
                new ReportMetric(S["Reserved"].Value, ReportFormat.Number(open.LongCount(activity => activity.AssignmentStatus == ActivityAssignmentStatus.Reserved))),
            ]))
            .Add(BuildProgressSection(open, S["Backlog by status"].Value, S["Status"].Value, activity => activity.Status.ToString()));
    }

    private ReportDocument BuildAging(IReadOnlyList<OmnichannelActivityIndex> activities, DateTime asOfUtc)
    {
        var buckets = new[]
        {
            new ActivityAgeBucket(S["Under 1 hour"].Value, 0, 1d / 24d),
            new ActivityAgeBucket(S["1-4 hours"].Value, 1d / 24d, 4d / 24d),
            new ActivityAgeBucket(S["4-24 hours"].Value, 4d / 24d, 1),
            new ActivityAgeBucket(S["1-3 days"].Value, 1, 3),
            new ActivityAgeBucket(S["3-7 days"].Value, 3, 7),
            new ActivityAgeBucket(S["7+ days"].Value, 7, double.MaxValue),
        };

        var open = activities.Where(activity => !IsTerminal(activity.Status)).ToArray();
        var columns = new[]
        {
            new ReportColumn(S["Age bucket"].Value),
            new ReportColumn(S["Activities"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Share"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Unassigned"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Overdue"].Value, ReportColumnAlign.End),
        };

        var rows = buckets.Reverse().Select(bucket =>
        {
            var matching = open.Where(activity =>
            {
                var age = Math.Max(0d, (asOfUtc - activity.CreatedUtc).TotalDays);

                return age >= bucket.FromDays && age < bucket.ToDays;
            }).ToArray();

            return new ReportRow(
            [
                bucket.Label,
                ReportFormat.Number(matching.LongLength),
                ReportFormat.Percent(open.Length > 0 ? (double)matching.LongLength / open.Length : 0d),
                ReportFormat.Number(matching.LongCount(activity => string.IsNullOrEmpty(activity.AssignedToId))),
                ReportFormat.Number(matching.LongCount(activity => IsOverdue(activity, asOfUtc))),
            ]);
        });

        return new ReportDocument()
            .Add(ReportSection.ForTable(S["Activity aging"].Value, columns, rows));
    }

    private ReportDocument BuildProgress(
        IReadOnlyList<OmnichannelActivityIndex> activities,
        string dimensionName,
        Func<OmnichannelActivityIndex, string> selector)
    {
        return new ReportDocument()
            .Add(BuildProgressSection(activities, S["Activity performance"].Value, dimensionName, selector));
    }

    private ReportSection BuildProgressSection(
        IEnumerable<OmnichannelActivityIndex> activities,
        string title,
        string dimensionName,
        Func<OmnichannelActivityIndex, string> selector)
    {
        var columns = new[]
        {
            new ReportColumn(dimensionName),
            new ReportColumn(S["Total"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Completed"].Value, ReportColumnAlign.End),
            new ReportColumn(S["In progress"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Pending"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Failed"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Cancelled"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Completion rate"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Avg attempts"].Value, ReportColumnAlign.End),
        };

        var rows = activities
            .GroupBy(selector, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var counts = Aggregate(group);

                return new
                {
                    counts.Total,
                    Row = new ReportRow(
                    [
                        group.Key,
                        ReportFormat.Number(counts.Total),
                        ReportFormat.Number(counts.Completed),
                        ReportFormat.Number(counts.InProgress),
                        ReportFormat.Number(counts.Pending),
                        ReportFormat.Number(counts.Failed),
                        ReportFormat.Number(counts.Cancelled),
                        ReportFormat.Percent(counts.CompletionRate),
                        counts.Total > 0 ? ((double)counts.Attempts / counts.Total).ToString("N2", System.Globalization.CultureInfo.CurrentCulture) : "0.00",
                    ]),
                };
            })
            .OrderByDescending(entry => entry.Total)
            .Select(entry => entry.Row);

        return ReportSection.ForTable(title, columns, rows);
    }

    private ReportDocument BuildAttempts(IReadOnlyList<OmnichannelActivityIndex> activities)
    {
        var columns = new[]
        {
            new ReportColumn(S["Attempts"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Activities"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Completed"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Failed"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Completion rate"].Value, ReportColumnAlign.End),
        };

        var rows = activities
            .GroupBy(activity => Math.Max(0, activity.Attempts))
            .OrderBy(group => group.Key)
            .Select(group =>
            {
                var total = group.LongCount();
                var completed = group.LongCount(activity => activity.Status == ActivityStatus.Completed);

                return new ReportRow(
                [
                    ReportFormat.Number(group.Key),
                    ReportFormat.Number(total),
                    ReportFormat.Number(completed),
                    ReportFormat.Number(group.LongCount(activity => activity.Status == ActivityStatus.Failed)),
                    ReportFormat.Percent(total > 0 ? (double)completed / total : 0d),
                ]);
            });

        return new ReportDocument()
            .Add(ReportSection.ForTable(S["Attempt distribution"].Value, columns, rows));
    }

    private ReportDocument BuildCompletionDuration(
        IReadOnlyList<OmnichannelActivityIndex> activities,
        string dimensionName,
        Func<OmnichannelActivityIndex, string> selector)
    {
        var completed = activities.Where(activity => activity.CompletedUtc.HasValue);
        var columns = new[]
        {
            new ReportColumn(dimensionName),
            new ReportColumn(S["Completed"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Average cycle time"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Median cycle time"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Maximum cycle time"].Value, ReportColumnAlign.End),
        };
        var rows = completed
            .GroupBy(selector, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var durations = group
                    .Select(activity => Math.Max(0d, (activity.CompletedUtc.Value - activity.CreatedUtc).TotalSeconds))
                    .OrderBy(value => value)
                    .ToArray();
                var median = durations.Length == 0
                    ? 0d
                    : durations.Length % 2 == 1
                        ? durations[durations.Length / 2]
                        : (durations[(durations.Length / 2) - 1] + durations[durations.Length / 2]) / 2d;

                return new
                {
                    Average = durations.Length > 0 ? durations.Average() : 0d,
                    Row = new ReportRow(
                    [
                        group.Key,
                        ReportFormat.Number(durations.LongLength),
                        ReportFormat.Duration(durations.Length > 0 ? durations.Average() : 0d),
                        ReportFormat.Duration(median),
                        ReportFormat.Duration(durations.Length > 0 ? durations.Max() : 0d),
                    ]),
                };
            })
            .OrderByDescending(entry => entry.Average)
            .Select(entry => entry.Row);

        return new ReportDocument()
            .Add(ReportSection.ForTable(S["Activity completion time"].Value, columns, rows));
    }

    private ReportDocument BuildDailyUserProductivity(
        IReadOnlyList<OmnichannelActivityIndex> activities,
        IReadOnlyDictionary<string, string> users)
    {
        var completed = activities.Where(activity => activity.CompletedUtc.HasValue && !string.IsNullOrEmpty(activity.AssignedToId));
        var columns = new[]
        {
            new ReportColumn(S["Date (UTC)"].Value),
            new ReportColumn(S["Assigned user"].Value),
            new ReportColumn(S["Completed"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Average cycle time"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Attempts"].Value, ReportColumnAlign.End),
        };
        var rows = completed
            .GroupBy(activity => new
            {
                Date = DateOnly.FromDateTime(activity.CompletedUtc.Value),
                activity.AssignedToId,
            })
            .OrderBy(group => group.Key.Date)
            .ThenBy(group => group.Key.AssignedToId, StringComparer.OrdinalIgnoreCase)
            .Select(group => new ReportRow(
            [
                group.Key.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ResolveUser(group.Key.AssignedToId, users),
                ReportFormat.Number(group.LongCount()),
                ReportFormat.Duration(group.Average(activity => Math.Max(0d, (activity.CompletedUtc.Value - activity.CreatedUtc).TotalSeconds))),
                ReportFormat.Number(group.Sum(activity => Math.Max(0, activity.Attempts))),
            ]));

        return new ReportDocument()
            .Add(ReportSection.ForTable(S["Daily user productivity"].Value, columns, rows));
    }

    private ReportDocument BuildCampaignDimension(
        IReadOnlyList<OmnichannelActivityIndex> activities,
        string dimensionName,
        Func<OmnichannelActivityIndex, string> selector)
    {
        var columns = new[]
        {
            new ReportColumn(S["Campaign"].Value),
            new ReportColumn(dimensionName),
            new ReportColumn(S["Activities"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Completed"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Failed"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Completion rate"].Value, ReportColumnAlign.End),
        };
        var rows = activities
            .GroupBy(activity => new
            {
                Campaign = Display(activity.CampaignId),
                Dimension = selector(activity),
            })
            .Select(group =>
            {
                var total = group.LongCount();
                var completed = group.LongCount(activity => activity.Status == ActivityStatus.Completed);

                return new
                {
                    Total = total,
                    Row = new ReportRow(
                    [
                        group.Key.Campaign,
                        group.Key.Dimension,
                        ReportFormat.Number(total),
                        ReportFormat.Number(completed),
                        ReportFormat.Number(group.LongCount(activity => activity.Status == ActivityStatus.Failed)),
                        ReportFormat.Percent(total > 0 ? (double)completed / total : 0d),
                    ]),
                };
            })
            .OrderByDescending(entry => entry.Total)
            .Select(entry => entry.Row);

        return new ReportDocument()
            .Add(ReportSection.ForTable(S["Campaign breakdown"].Value, columns, rows));
    }

    private ReportDocument BuildOverdueByDimension(
        IReadOnlyList<OmnichannelActivityIndex> activities,
        DateTime asOfUtc,
        string dimensionName,
        Func<OmnichannelActivityIndex, string> selector)
    {
        var overdue = activities.Where(activity => !IsTerminal(activity.Status) && IsOverdue(activity, asOfUtc));
        var columns = new[]
        {
            new ReportColumn(dimensionName),
            new ReportColumn(S["Overdue"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Unassigned"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Average overdue age"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Maximum overdue age"].Value, ReportColumnAlign.End),
        };
        var rows = overdue
            .GroupBy(selector, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var ages = group.Select(activity => Math.Max(0d, (asOfUtc - activity.ScheduledUtc).TotalSeconds)).ToArray();

                return new
                {
                    Count = ages.LongLength,
                    Row = new ReportRow(
                    [
                        group.Key,
                        ReportFormat.Number(ages.LongLength),
                        ReportFormat.Number(group.LongCount(activity => string.IsNullOrEmpty(activity.AssignedToId))),
                        ReportFormat.Duration(ages.Length > 0 ? ages.Average() : 0d),
                        ReportFormat.Duration(ages.Length > 0 ? ages.Max() : 0d),
                    ]),
                };
            })
            .OrderByDescending(entry => entry.Count)
            .Select(entry => entry.Row);

        return new ReportDocument()
            .Add(ReportSection.ForTable(S["Overdue workload"].Value, columns, rows));
    }

    private ReportDocument BuildScheduleCompletion(IReadOnlyList<OmnichannelActivityIndex> activities)
    {
        var completed = activities.Where(activity => activity.CompletedUtc.HasValue && activity.ScheduledUtc != default);
        var columns = new[]
        {
            new ReportColumn(S["Schedule result"].Value),
            new ReportColumn(S["Activities"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Share"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Average variance"].Value, ReportColumnAlign.End),
        };
        var groups = completed.GroupBy(activity => activity.CompletedUtc.Value <= activity.ScheduledUtc ? S["Completed by schedule"].Value : S["Completed late"].Value);
        var total = completed.LongCount();
        var rows = groups.Select(group =>
        {
            var variances = group
                .Select(activity => Math.Abs((activity.CompletedUtc.Value - activity.ScheduledUtc).TotalSeconds))
                .ToArray();

            return new ReportRow(
            [
                group.Key,
                ReportFormat.Number(group.LongCount()),
                ReportFormat.Percent(total > 0 ? (double)group.LongCount() / total : 0d),
                ReportFormat.Duration(variances.Length > 0 ? variances.Average() : 0d),
            ]);
        });

        return new ReportDocument()
            .Add(ReportSection.ForTable(S["Scheduled completion performance"].Value, columns, rows));
    }

    private string Display(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? S["(Not set)"].Value : value;
    }

    private async Task<IReadOnlyDictionary<string, string>> ResolveUsersAsync(
        IEnumerable<OmnichannelActivityIndex> activities,
        CancellationToken cancellationToken)
    {
        var userIds = activities
            .SelectMany(activity => new[] { activity.AssignedToId, activity.CreatedById })
            .Where(userId => !string.IsNullOrEmpty(userId))
            .Distinct(StringComparer.Ordinal);
        var users = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var userId in userIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var user = await _userManager.FindByIdAsync(userId);

            if (user is not null)
            {
                users[userId] = await _userManager.GetUserNameAsync(user) ?? userId;
            }
        }

        return users;
    }

    private string ResolveUser(string userId, IReadOnlyDictionary<string, string> users)
    {
        return !string.IsNullOrEmpty(userId) && users.TryGetValue(userId, out var userName)
            ? userName
            : Display(userId);
    }

    private static ActivityProgress Aggregate(IEnumerable<OmnichannelActivityIndex> activities)
    {
        var result = new ActivityProgress();

        foreach (var activity in activities)
        {
            result.Total++;
            result.Attempts += Math.Max(0, activity.Attempts);

            switch (activity.Status)
            {
                case ActivityStatus.Completed:
                    result.Completed++;

                    break;
                case ActivityStatus.Failed:
                    result.Failed++;

                    break;
                case ActivityStatus.Cancelled:
                case ActivityStatus.Purged:
                    result.Cancelled++;

                    break;
                case ActivityStatus.AwaitingAgentResponse:
                case ActivityStatus.AwaitingCustomerAnswer:
                case ActivityStatus.Reserved:
                case ActivityStatus.Dialing:
                case ActivityStatus.InProgress:
                    result.InProgress++;

                    break;
                default:
                    result.Pending++;

                    break;
            }
        }

        return result;
    }

    private static bool IsTerminal(ActivityStatus status)
    {
        return status is ActivityStatus.Completed or ActivityStatus.Failed or ActivityStatus.Cancelled or ActivityStatus.Purged;
    }

    private static bool IsOverdue(OmnichannelActivityIndex activity, DateTime asOfUtc)
    {
        return activity.ScheduledUtc != default && activity.ScheduledUtc < asOfUtc;
    }

    private sealed record ActivityAgeBucket(string Label, double FromDays, double ToDays);

    private sealed class ActivityProgress
    {
        public long Total { get; set; }

        public long Completed { get; set; }

        public long InProgress { get; set; }

        public long Pending { get; set; }

        public long Failed { get; set; }

        public long Cancelled { get; set; }

        public long Attempts { get; set; }

        public double CompletionRate => Total > 0 ? (double)Completed / Total : 0d;
    }
}

internal sealed record EnterpriseActivityReportDefinition(
    string Name,
    Func<LocalizedString> DisplayName,
    Func<LocalizedString> Description,
    EnterpriseActivityReportKind Kind,
    string Category);

internal enum EnterpriseActivityReportKind
{
    Backlog,
    Aging,
    SourcePerformance,
    ChannelPerformance,
    KindPerformance,
    AssignmentPerformance,
    AttemptAnalysis,
    ContactTypeWorkload,
    UrgencyPerformance,
    AssignedUserPerformance,
    CreatedByPerformance,
    UserCompletionTime,
    UserDailyProductivity,
    CampaignSourceMix,
    CampaignChannelMix,
    CampaignDispositionMix,
    CampaignAttemptPerformance,
    OverdueByUser,
    ChannelEndpointUsage,
    CustomerWorkload,
    ScheduleCompletion,
}
