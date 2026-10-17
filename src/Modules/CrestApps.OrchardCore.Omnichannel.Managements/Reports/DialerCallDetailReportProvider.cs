using System.Globalization;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Drivers;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Localization;
using OrchardCore.Security.Permissions;
using OrchardCore.Users.Indexes;
using OrchardCore.Users.Models;
using YesSql;
using YesSql.Services;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Reports;

/// <summary>
/// The Dialer call detail report: one row per dialed activity, with the number dialed, its contact and campaign, the
/// disposition it ended with, its attempts, when it completed, its agent and its status. The rows are read from the
/// activity index a page at a time, so a campaign of any size can be listed; an export reads every matching row.
/// </summary>
public sealed class DialerCallDetailReportProvider : OmnichannelReportBase, IReportFilterMetadata, IOmnichannelReportFilterDefaults, IReport
{
    /// <summary>
    /// The report's technical name.
    /// </summary>
    public const string ReportName = "omnichannel-dialer-call-detail";

    /// <summary>
    /// The number of rows one page shows.
    /// </summary>
    public const int PageSize = 100;

    /// <summary>
    /// The number of rows an export reads per query, so no single query loads the whole result set.
    /// </summary>
    internal const int ExportBatchSize = 500;

    /// <summary>
    /// The number of identifiers one name lookup binds, kept under the smallest parameter limit a database enforces.
    /// </summary>
    private const int LookupBatchSize = 500;

    private static readonly string[] _filterNames =
    [
        OmnichannelReportFilterDisplayDriver.FilterName,
        OmnichannelReportFilterDisplayDriver.PageFilterName,
    ];

    private readonly ISession _session;
    private readonly ICatalogManager<OmnichannelCampaign> _campaignManager;
    private readonly INamedCatalogManager<OmnichannelDisposition> _dispositionManager;
    private readonly ActivitySourceOptions _activitySourceOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialerCallDetailReportProvider"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="campaignManager">The campaign manager used to resolve campaign names.</param>
    /// <param name="dispositionManager">The disposition manager used to resolve disposition names.</param>
    /// <param name="activitySourceOptions">The activity sources registered by the enabled features.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DialerCallDetailReportProvider(
        ISession session,
        ICatalogManager<OmnichannelCampaign> campaignManager,
        INamedCatalogManager<OmnichannelDisposition> dispositionManager,
        IOptions<ActivitySourceOptions> activitySourceOptions,
        IStringLocalizer<DialerCallDetailReportProvider> stringLocalizer)
        : base(stringLocalizer)
    {
        _session = session;
        _campaignManager = campaignManager;
        _dispositionManager = dispositionManager;
        _activitySourceOptions = activitySourceOptions.Value;
    }

    /// <inheritdoc/>
    public static string Name => ReportName;

    /// <inheritdoc/>
    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<DialerCallDetailReportProvider>("Dialer call detail");

    /// <inheritdoc/>
    public static LocalizationSource Description { get; } = LocalizationSource.Create<DialerCallDetailReportProvider>("One row per dialed number with its contact, campaign, last disposition, attempts, completion time, agent and status.");

    /// <inheritdoc/>
    public static string Category => ReportsConstants.Categories.CrmCampaigns;

    /// <inheritdoc/>
    public static Permission Permission => OmnichannelConstants.Permissions.ViewReports;

    /// <inheritdoc/>
    public IReadOnlyCollection<string> FilterNames => _filterNames;

    /// <inheritdoc/>
    public void ApplyDefaults(ReportFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);

        filter.Set(OmnichannelReportFilter.Source, ActivitySources.Dialer);
    }

    /// <inheritdoc/>
    public override async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var criteria = await OmnichannelReportFilter.GetCriteriaAsync(context.Filter, _campaignManager, _activitySourceOptions, cancellationToken);

        if (criteria.CampaignIds is { Count: 0 })
        {
            // The selected campaign group has no campaigns, so no activity can match.
            return new ReportDocument();
        }

        var range = context.Filter.GetDateRange();
        var fromUtc = OmnichannelReportQuery.ClampToQueryableRange(range.FromUtc.GetValueOrDefault());
        var toUtc = OmnichannelReportQuery.ClampToQueryableRange(range.ToUtc.GetValueOrDefault());

        // A completed attempt belongs to the day it completed. An attempt that has not completed, whether it is
        // waiting to be dialed or being dialed now, belongs to the day it is scheduled for.
        IQueryIndex<OmnichannelActivityIndex> CreateQuery()
        {
            return ApplyCriteria(
                _session.QueryIndex<OmnichannelActivityIndex>(
                    index => (index.CompletedUtc >= fromUtc && index.CompletedUtc <= toUtc) ||
                        (index.CompletedUtc == null && index.ScheduledUtc >= fromUtc && index.ScheduledUtc <= toUtc),
                    collection: OmnichannelConstants.CollectionName),
                criteria);
        }

        IReadOnlyList<OmnichannelActivityIndex> activities;
        string description;

        if (context.Mode == ReportRunMode.Export)
        {
            activities = await ReadAllAsync(CreateQuery, cancellationToken);
            description = S["{0} rows.", ReportFormat.Number(activities.Count)].Value;
        }
        else
        {
            var total = await CreateQuery().CountAsync(cancellationToken);

            if (total == 0)
            {
                return new ReportDocument();
            }

            var pageCount = (int)Math.Ceiling(total / (double)PageSize);
            var page = Math.Min(criteria.Page, pageCount);
            var skip = (page - 1) * PageSize;

            activities = await CreateQuery()
                .OrderByDescending(index => index.DocumentId)
                .Skip(skip)
                .Take(PageSize)
                .ListAsync(cancellationToken);
            description = S["Page {0} of {1}: rows {2} to {3} of {4}, newest first. An export includes every row.",
                ReportFormat.Number(page),
                ReportFormat.Number(pageCount),
                ReportFormat.Number(skip + 1),
                ReportFormat.Number(skip + activities.Count),
                ReportFormat.Number(total)].Value;
        }

        if (activities.Count == 0)
        {
            return new ReportDocument();
        }

        var campaignNames = CatalogReportDisplayNames.ForCampaigns(await _campaignManager.GetAllAsync(cancellationToken));
        var dispositionNames = CatalogReportDisplayNames.ForDispositions(await _dispositionManager.GetAllAsync(cancellationToken));
        var contactNames = await ResolveContactNamesAsync(activities, cancellationToken);
        var userNames = await ResolveUserNamesAsync(activities, cancellationToken);
        var noCampaign = S["(No campaign)"].Value;
        var unknownCampaign = S["(Unknown campaign)"].Value;
        var noDisposition = S["(No disposition)"].Value;
        var unknownDisposition = S["(Unknown disposition)"].Value;

        var columns = new[]
        {
            new ReportColumn(S["Phone number"].Value),
            new ReportColumn(S["Contact"].Value),
            new ReportColumn(S["Campaign"].Value),
            new ReportColumn(S["Last disposition"].Value),
            new ReportColumn(S["Attempts"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Completed (UTC)"].Value),
            new ReportColumn(S["Agent"].Value),
            new ReportColumn(S["Status"].Value),
        };

        var rows = activities.Select(activity => new ReportRow(
        [
            Display(activity.PreferredDestination),
            ResolveContact(activity.ContactContentItemId, contactNames),
            CatalogReportDisplayNames.Resolve(activity.CampaignId, campaignNames, noCampaign, unknownCampaign),
            CatalogReportDisplayNames.Resolve(activity.DispositionId, dispositionNames, noDisposition, unknownDisposition),
            ReportFormat.Number(Math.Max(0, activity.Attempts)),
            activity.CompletedUtc.HasValue
                ? activity.CompletedUtc.Value.ToString("u", CultureInfo.InvariantCulture)
                : string.Empty,
            ResolveUser(activity.AssignedToId, userNames),
            GetStatusName(activity.Status),
        ]));

        var section = ReportSection.ForTable(S["Dialed numbers"].Value, columns, rows);
        section.Description = description;

        return new ReportDocument().Add(section);
    }

    private static IQueryIndex<OmnichannelActivityIndex> ApplyCriteria(
        IQueryIndex<OmnichannelActivityIndex> query,
        OmnichannelReportCriteria criteria)
    {
        if (!string.IsNullOrEmpty(criteria.CampaignId))
        {
            var campaignId = criteria.CampaignId;
            query = query.Where(index => index.CampaignId == campaignId);
        }

        if (criteria.CampaignIds is not null)
        {
            var campaignIds = criteria.CampaignIds.ToArray();
            query = query.Where(index => index.CampaignId.IsIn(campaignIds));
        }

        if (!string.IsNullOrEmpty(criteria.Channel))
        {
            var channel = criteria.Channel;
            query = query.Where(index => index.Channel == channel);
        }

        if (criteria.Sources is not null)
        {
            var sources = criteria.Sources.ToArray();
            query = query.Where(index => index.Source.IsIn(sources));
        }
        else if (!string.IsNullOrEmpty(criteria.Source))
        {
            var source = criteria.Source;
            query = query.Where(index => index.Source == source);
        }

        if (criteria.Status.HasValue)
        {
            var status = criteria.Status.Value;
            query = query.Where(index => index.Status == status);
        }

        if (criteria.DispositionIds is not null)
        {
            var dispositionIds = criteria.DispositionIds.ToArray();
            query = query.Where(index => index.DispositionId.IsIn(dispositionIds));
        }

        return query;
    }

    private static async Task<IReadOnlyList<OmnichannelActivityIndex>> ReadAllAsync(
        Func<IQueryIndex<OmnichannelActivityIndex>> createQuery,
        CancellationToken cancellationToken)
    {
        // Every batch continues from the last document the previous one returned, so a batch never has to skip over
        // the rows already read.
        var activities = new List<OmnichannelActivityIndex>();
        var cursor = long.MaxValue;

        while (true)
        {
            var lastDocumentId = cursor;
            var batch = await createQuery()
                .Where(index => index.DocumentId < lastDocumentId)
                .OrderByDescending(index => index.DocumentId)
                .Take(ExportBatchSize)
                .ListAsync(cancellationToken);

            activities.AddRange(batch);

            if (batch.Count < ExportBatchSize)
            {
                return activities;
            }

            cursor = batch[batch.Count - 1].DocumentId;
        }
    }

    private async Task<IReadOnlyDictionary<string, string>> ResolveContactNamesAsync(
        IEnumerable<OmnichannelActivityIndex> activities,
        CancellationToken cancellationToken)
    {
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        var contactIds = activities
            .Select(activity => activity.ContactContentItemId)
            .Where(contactId => !string.IsNullOrEmpty(contactId))
            .Distinct(StringComparer.Ordinal);

        foreach (var batch in contactIds.Chunk(LookupBatchSize))
        {
            var contacts = await _session.Query<ContentItem, ContentItemIndex>(
                    index => index.ContentItemId.IsIn(batch) && index.Latest)
                .ListAsync(cancellationToken);

            foreach (var contact in contacts)
            {
                names[contact.ContentItemId] = string.IsNullOrWhiteSpace(contact.DisplayText)
                    ? contact.ContentItemId
                    : contact.DisplayText.Trim();
            }
        }

        return names;
    }

    private async Task<IReadOnlyDictionary<string, string>> ResolveUserNamesAsync(
        IEnumerable<OmnichannelActivityIndex> activities,
        CancellationToken cancellationToken)
    {
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        var userIds = activities
            .Select(activity => activity.AssignedToId)
            .Where(userId => !string.IsNullOrEmpty(userId))
            .Distinct(StringComparer.Ordinal);

        foreach (var batch in userIds.Chunk(LookupBatchSize))
        {
            var users = await _session.Query<User, UserIndex>(index => index.UserId.IsIn(batch))
                .ListAsync(cancellationToken);

            foreach (var user in users)
            {
                names[user.UserId] = user.UserName;
            }
        }

        return names;
    }

    private string ResolveContact(string contactId, IReadOnlyDictionary<string, string> contactNames)
    {
        if (string.IsNullOrEmpty(contactId))
        {
            return S["(No contact)"].Value;
        }

        return contactNames.TryGetValue(contactId, out var name)
            ? name
            : contactId;
    }

    private string ResolveUser(string userId, IReadOnlyDictionary<string, string> userNames)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return S["(Unassigned)"].Value;
        }

        var unknownUser = S["(Unknown user)"].Value;

        return userNames.TryGetValue(userId, out var userName)
            ? ReportValue.UserDisplayName(userName, unknownUser)
            : unknownUser;
    }

    private string Display(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? S["(Not set)"].Value
            : value;
    }

    private string GetStatusName(ActivityStatus status)
    {
        return status switch
        {
            ActivityStatus.NotStated => S["Not started"].Value,
            ActivityStatus.AwaitingAgentResponse => S["Awaiting agent response"].Value,
            ActivityStatus.AwaitingCustomerAnswer => S["Awaiting customer answer"].Value,
            ActivityStatus.Completed => S["Completed"].Value,
            ActivityStatus.Pending => S["Pending"].Value,
            ActivityStatus.Scheduled => S["Scheduled"].Value,
            ActivityStatus.Reserved => S["Reserved"].Value,
            ActivityStatus.Dialing => S["Dialing"].Value,
            ActivityStatus.InProgress => S["In progress"].Value,
            ActivityStatus.Failed => S["Failed"].Value,
            ActivityStatus.Cancelled => S["Cancelled"].Value,
            ActivityStatus.Purged => S["Purged"].Value,
            _ => status.ToString(),
        };
    }
}
