using System.Runtime.CompilerServices;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using OrchardCore.Localization;
using YesSql;
using YesSql.Services;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Reports.DataSources;

/// <summary>
/// Exposes Omnichannel records to reports and data pipelines: activities (including the dialer's work), dispositions,
/// campaigns, campaign groups, and activity batches. Every data set requires the View Omnichannel reports permission.
/// </summary>
public sealed class OmnichannelReportDataSource : RecordDataSource, IDataSource
{
    /// <summary>
    /// The technical name of the data source.
    /// </summary>
    public const string SourceName = "Omnichannel";

    private readonly IRecordDataSet[] _dataSets;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelReportDataSource"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="dispositions">The disposition catalog.</param>
    /// <param name="campaigns">The campaign catalog.</param>
    /// <param name="campaignGroups">The campaign group catalog.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public OmnichannelReportDataSource(
        ISession session,
        INamedCatalogManager<OmnichannelDisposition> dispositions,
        ICatalogManager<OmnichannelCampaign> campaigns,
        ICatalogManager<OmnichannelCampaignGroup> campaignGroups,
        IAuthorizationService authorizationService,
        IStringLocalizer<OmnichannelReportDataSource> stringLocalizer)
    {
        var S = stringLocalizer;
        var access = new OmnichannelReportAccess(authorizationService);

        _dataSets =
        [
            new ActivitiesDataSet(session, dispositions, campaigns, access, S),
            new DispositionsDataSet(dispositions, access, S),
            new CampaignsDataSet(campaigns, campaignGroups, access, S),
            new CampaignGroupsDataSet(campaignGroups, access, S),
            new ActivityBatchesDataSet(session, access, S),
        ];
    }

    /// <inheritdoc/>
    public static string Name => SourceName;

    /// <inheritdoc/>
    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<OmnichannelReportDataSource>("Omnichannel");

    /// <inheritdoc/>
    public static LocalizationSource Description { get; } = LocalizationSource.Create<OmnichannelReportDataSource>("Activities, including the dialer's calls, with their dispositions, campaigns, and batches.");

    /// <inheritdoc/>
    protected override IEnumerable<IRecordDataSet> DataSets => _dataSets;

    internal static DataFieldReference Users()
    {
        return new DataFieldReference(ReportsConstants.UsersDataSource, ReportsConstants.UsersDataSet, ReportsConstants.UserIdField);
    }

    internal static DataFieldReference Own(string dataSet)
    {
        return new DataFieldReference(SourceName, dataSet, "ItemId");
    }
}

/// <summary>
/// Decides who may read the Omnichannel data sets.
/// </summary>
internal sealed class OmnichannelReportAccess
{
    private readonly IAuthorizationService _authorizationService;

    public OmnichannelReportAccess(IAuthorizationService authorizationService)
    {
        _authorizationService = authorizationService;
    }

    public async Task<bool> CanReadAsync(DataSourceContext context)
    {
        return context?.User is not null &&
            await _authorizationService.AuthorizeAsync(context.User, OmnichannelConstants.Permissions.ViewReports);
    }
}

/// <summary>
/// One row per activity: a task, call, message, or any other unit of work, including the dialer's attempts.
/// </summary>
internal sealed class ActivitiesDataSet : RecordDataSet<OmnichannelActivity>, IAggregateRecordDataSet
{
    public const string Name = "Activities";

    // Index columns a join can narrow the read on.
    private static readonly string[] _keyFields = ["ItemId", "AssignedToId", "CreatedById", "DispositionId", "CampaignId", "ContactContentItemId"];

    private readonly ISession _session;
    private readonly INamedCatalogManager<OmnichannelDisposition> _dispositions;
    private readonly ICatalogManager<OmnichannelCampaign> _campaigns;
    private readonly OmnichannelReportAccess _access;

    public ActivitiesDataSet(
        ISession session,
        INamedCatalogManager<OmnichannelDisposition> dispositions,
        ICatalogManager<OmnichannelCampaign> campaigns,
        OmnichannelReportAccess access,
        IStringLocalizer S)
        : base(new DataSetDescriptor(Name, S["Activities"], S["One row per activity: tasks, calls, messages, and the dialer's attempts."]) { DefaultDateField = "CreatedUtc" })
    {
        _session = session;
        _dispositions = dispositions;
        _campaigns = campaigns;
        _access = access;

        var activity = S["Activity"].Value;
        var people = S["People"].Value;
        var outcome = S["Outcome"].Value;
        var dates = S["Dates"].Value;
        var links = S["Links"].Value;

        AddField("ItemId", S["Activity ID"], DataFieldType.Text, record => record.ItemId, activity).Identifier();
        AddField("Kind", S["Kind"], DataFieldType.Text, record => record.Kind, activity);
        AddField("Source", S["Source"], DataFieldType.Text, record => record.Source, activity);
        AddField("Channel", S["Channel"], DataFieldType.Text, record => record.Channel, activity);
        AddField("InteractionType", S["Interaction type"], DataFieldType.Text, record => record.InteractionType, activity);
        AddField("Status", S["Status"], DataFieldType.Text, record => record.Status, activity);
        AddField("UrgencyLevel", S["Urgency"], DataFieldType.Text, record => record.UrgencyLevel, activity);
        AddField("Attempts", S["Attempts"], DataFieldType.Integer, record => record.Attempts, activity);
        AddField("ProcessingAttempts", S["Processing attempts"], DataFieldType.Integer, record => record.ProcessingAttempts, activity);
        AddField("PreferredDestination", S["Destination"], DataFieldType.Text, record => record.PreferredDestination, activity);
        AddField("SubjectContentType", S["Subject type"], DataFieldType.Text, record => record.SubjectContentType, activity);
        AddField("AiEscalated", S["Escalated by AI"], DataFieldType.Boolean, record => record.AiEscalated, activity);

        AddField("AssignmentStatus", S["Assignment status"], DataFieldType.Text, record => record.AssignmentStatus, people);
        AddField("AssignedToId", S["Assigned to (user ID)"], DataFieldType.Text, record => record.AssignedToId, people).Identifier(OmnichannelReportDataSource.Users());
        AddField("AssignedToUsername", S["Assigned to"], DataFieldType.Text, record => record.AssignedToUsername, people);
        AddField("CreatedById", S["Created by (user ID)"], DataFieldType.Text, record => record.CreatedById, people).Identifier(OmnichannelReportDataSource.Users());
        AddField("CreatedByUsername", S["Created by"], DataFieldType.Text, record => record.CreatedByUsername, people);
        AddField("CompletedById", S["Completed by (user ID)"], DataFieldType.Text, record => record.CompletedById, people).Identifier(OmnichannelReportDataSource.Users());
        AddField("CompletedByUsername", S["Completed by"], DataFieldType.Text, record => record.CompletedByUsername, people);

        AddField("DispositionId", S["Disposition ID"], DataFieldType.Text, record => record.DispositionId, outcome).Identifier(OmnichannelReportDataSource.Own(DispositionsDataSet.Name));
        AddField("Disposition", S["Disposition"], DataFieldType.Text, record => Lookup(Dispositions, record.DispositionId), outcome);
        AddField("DispositionedBy", S["Dispositioned by"], DataFieldType.Text, record => record.DispositionedBy, outcome);
        AddField("TerminalReasonCode", S["Terminal reason"], DataFieldType.Text, record => record.TerminalReasonCode, outcome);

        AddField("CreatedUtc", S["Created"], DataFieldType.DateTime, record => record.CreatedUtc, dates);
        AddField("ScheduledUtc", S["Scheduled"], DataFieldType.DateTime, record => record.ScheduledUtc, dates);
        AddField("AssignedToUtc", S["Assigned"], DataFieldType.DateTime, record => record.AssignedToUtc, dates);
        AddField("CompletedUtc", S["Completed"], DataFieldType.DateTime, record => record.CompletedUtc, dates);
        AddField("HandleMinutes", S["Minutes to complete"], DataFieldType.Decimal, record => record.CompletedUtc.HasValue && record.CompletedUtc.Value >= record.CreatedUtc
            ? Math.Round((decimal)(record.CompletedUtc.Value - record.CreatedUtc).TotalMinutes, 2)
            : null, dates);
        AddField("HandoffSummaryUtc", S["Handed off by AI"], DataFieldType.DateTime, record => record.HandoffSummaryUtc, dates);

        AddField("CampaignId", S["Campaign ID"], DataFieldType.Text, record => record.CampaignId, links).Identifier(OmnichannelReportDataSource.Own(CampaignsDataSet.Name));
        AddField("Campaign", S["Campaign"], DataFieldType.Text, record => Lookup(Campaigns, record.CampaignId), links);
        AddField("ContactContentItemId", S["Contact ID"], DataFieldType.Text, record => record.ContactContentItemId, links).Identifier();
        AddField("ContactContentType", S["Contact type"], DataFieldType.Text, record => record.ContactContentType, links);
        AddField("ContactResolutionStatus", S["Contact match"], DataFieldType.Text, record => record.ContactResolutionStatus, links);

        // Contacts and leads are content items of the types the site defines, so their identifiers join the Contents
        // data source on the content type a report picks, not on a reference declared here.
        AddField("ConvertedFromLeadItemId", S["Converted from lead ID"], DataFieldType.Text, record => record.ConvertedFromLeadItemId, links).Identifier();

        // The dialer and chat data sets live in other modules; these references join to them when they are enabled.
        AddField("DialerProfileId", S["Dialer profile ID"], DataFieldType.Text, record => record.DialerProfileId, links).Identifier(new DataFieldReference("ContactCenter", "DialerProfiles", "ItemId"));
        AddField("AISessionId", S["AI chat session ID"], DataFieldType.Text, record => record.AISessionId, links).Identifier(new DataFieldReference("AIChat", "ChatSessions", "SessionId"));
        AddField("AIProfileId", S["AI profile ID"], DataFieldType.Text, record => record.AIProfileId, links).Identifier();
        AddField("CadenceId", S["Cadence ID"], DataFieldType.Text, record => record.CadenceId, links).Identifier();

        Fields.MarkKeyFilterable(_keyFields);
    }

    // The names of the dispositions and campaigns, read once per query.
    private Dictionary<string, string> Dispositions { get; set; } = [];

    private Dictionary<string, string> Campaigns { get; set; } = [];

    public override Task<bool> CanReadAsync(DataSourceContext context, CancellationToken cancellationToken = default)
    {
        return _access.CanReadAsync(context);
    }

    // The fields the activity index copies unchanged, which the database can group, aggregate, and filter on.
    private IReadOnlyDictionary<string, string> _aggregateColumns;

    public Task<IReadOnlyList<object[]>> AggregateAsync(DataAggregateQuery query, CancellationToken cancellationToken = default)
    {
        _aggregateColumns ??= ReportIndexAggregator.MapByName<OmnichannelActivityIndex>(Fields.Select(field => field.Name));

        return ReportIndexAggregator.AggregateAsync<OmnichannelActivityIndex>(_session, OmnichannelConstants.CollectionName, query, _aggregateColumns, cancellationToken);
    }

    protected override async IAsyncEnumerable<OmnichannelActivity> LoadAsync(DataSourceQuery query, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (query.Fields is null || query.Fields.Count == 0 || query.Fields.Contains("Disposition"))
        {
            Dispositions = (await _dispositions.GetAllAsync(cancellationToken))
                .Where(disposition => !string.IsNullOrEmpty(disposition.ItemId))
                .ToDictionary(disposition => disposition.ItemId, disposition => disposition.Name, StringComparer.Ordinal);
        }

        if (query.Fields is null || query.Fields.Count == 0 || query.Fields.Contains("Campaign"))
        {
            Campaigns = (await _campaigns.GetAllAsync(cancellationToken))
                .Where(campaign => !string.IsNullOrEmpty(campaign.ItemId))
                .ToDictionary(campaign => campaign.ItemId, campaign => campaign.DisplayText, StringComparer.Ordinal);
        }

        var keyFilters = new List<(string Field, string[] Values)>();

        foreach (var field in _keyFields)
        {
            if (DataConditions.Keys(query.Conditions, field) is not { } keys)
            {
                continue;
            }

            if (keys.Count == 0)
            {
                yield break;
            }

            keyFilters.Add((field, keys.Select(key => key?.ToString()).ToArray()));
        }

        // The viewer's filters on the fields the index holds, such as a channel or a status, narrow the read itself.
        var columns = new ReportIndexConditions<OmnichannelActivityIndex>(query.Conditions)
            .Text("Source", nameof(OmnichannelActivityIndex.Source))
            .Text("Channel", nameof(OmnichannelActivityIndex.Channel))
            .Text("PreferredDestination", nameof(OmnichannelActivityIndex.PreferredDestination))
            .Text("SubjectContentType", nameof(OmnichannelActivityIndex.SubjectContentType))
            .Text("ContactContentType", nameof(OmnichannelActivityIndex.ContactContentType))
            .Text("TerminalReasonCode", nameof(OmnichannelActivityIndex.TerminalReasonCode))
            .Enum("Kind", index => index.Kind)
            .Enum("InteractionType", index => index.InteractionType)
            .Enum("Status", index => index.Status)
            .Enum("UrgencyLevel", index => index.UrgencyLevel)
            .Enum("AssignmentStatus", index => index.AssignmentStatus);

        if (columns.MatchesNothing)
        {
            yield break;
        }

        var (createdFrom, createdTo) = DataConditions.DateRange(query.Conditions, "CreatedUtc");
        var (scheduledFrom, scheduledTo) = DataConditions.DateRange(query.Conditions, "ScheduledUtc");
        var (completedFrom, completedTo) = DataConditions.DateRange(query.Conditions, "CompletedUtc");

        IQueryIndex<OmnichannelActivityIndex> Filter(IQueryIndex<OmnichannelActivityIndex> records)
        {
            records = columns.Apply(records);


            foreach (var (field, values) in keyFilters)
            {
                records = field switch
                {
                    "ItemId" => records.Where(index => index.ItemId.IsIn(values)),
                    "AssignedToId" => records.Where(index => index.AssignedToId.IsIn(values)),
                    "CreatedById" => records.Where(index => index.CreatedById.IsIn(values)),
                    "DispositionId" => records.Where(index => index.DispositionId.IsIn(values)),
                    "CampaignId" => records.Where(index => index.CampaignId.IsIn(values)),
                    _ => records.Where(index => index.ContactContentItemId.IsIn(values)),
                };
            }

            if (createdFrom.HasValue)
            {
                var value = createdFrom.Value;
                records = records.Where(index => index.CreatedUtc >= value);
            }

            if (createdTo.HasValue)
            {
                var value = createdTo.Value;
                records = records.Where(index => index.CreatedUtc <= value);
            }

            if (scheduledFrom.HasValue)
            {
                var value = scheduledFrom.Value;
                records = records.Where(index => index.ScheduledUtc >= value);
            }

            if (scheduledTo.HasValue)
            {
                var value = scheduledTo.Value;
                records = records.Where(index => index.ScheduledUtc <= value);
            }

            if (completedFrom.HasValue)
            {
                var value = completedFrom.Value;
                records = records.Where(index => index.CompletedUtc >= value);
            }

            if (completedTo.HasValue)
            {
                var value = completedTo.Value;
                records = records.Where(index => index.CompletedUtc <= value);
            }

            return records;
        }

        await foreach (var record in ReportIndexPages.ReadAsync<OmnichannelActivity, OmnichannelActivityIndex>(
            _session,
            Filter,
            index => index.CreatedUtc,
            OmnichannelConstants.CollectionName,
            cancellationToken))
        {
            yield return record;
        }
    }

    private static string Lookup(Dictionary<string, string> names, string id)
    {
        return id is not null && names.TryGetValue(id, out var name) ? name : null;
    }
}

/// <summary>
/// One row per disposition. Dispositions are grouped by their outcome.
/// </summary>
internal sealed class DispositionsDataSet : RecordDataSet<OmnichannelDisposition>
{
    public const string Name = "Dispositions";

    private readonly INamedCatalogManager<OmnichannelDisposition> _dispositions;
    private readonly OmnichannelReportAccess _access;

    public DispositionsDataSet(INamedCatalogManager<OmnichannelDisposition> dispositions, OmnichannelReportAccess access, IStringLocalizer S)
        : base(new DataSetDescriptor(Name, S["Dispositions"], S["The dispositions activities are closed with, grouped by outcome."]))
    {
        _dispositions = dispositions;
        _access = access;

        AddField("ItemId", S["Disposition ID"], DataFieldType.Text, record => record.ItemId).Identifier();
        AddField("Name", S["Name"], DataFieldType.Text, record => record.Name);
        AddField("Description", S["Description"], DataFieldType.Text, record => record.Description);
        AddField("Outcome", S["Outcome"], DataFieldType.Text, record => record.Outcome);
        AddField("CaptureDate", S["Asks for a date"], DataFieldType.Boolean, record => record.CaptureDate);
        AddField("CreatedUtc", S["Created"], DataFieldType.DateTime, record => record.CreatedUtc);
        AddField("ModifiedUtc", S["Modified"], DataFieldType.DateTime, record => record.ModifiedUtc);
    }

    public override Task<bool> CanReadAsync(DataSourceContext context, CancellationToken cancellationToken = default)
    {
        return _access.CanReadAsync(context);
    }

    protected override async IAsyncEnumerable<OmnichannelDisposition> LoadAsync(DataSourceQuery query, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var disposition in (await _dispositions.GetAllAsync(cancellationToken)).OrderBy(disposition => disposition.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            yield return disposition;
        }
    }
}

/// <summary>
/// One row per campaign.
/// </summary>
internal sealed class CampaignsDataSet : RecordDataSet<OmnichannelCampaign>
{
    public const string Name = "Campaigns";

    private readonly ICatalogManager<OmnichannelCampaign> _campaigns;
    private readonly ICatalogManager<OmnichannelCampaignGroup> _groups;
    private readonly OmnichannelReportAccess _access;
    private Dictionary<string, string> _groupNames = [];

    public CampaignsDataSet(ICatalogManager<OmnichannelCampaign> campaigns, ICatalogManager<OmnichannelCampaignGroup> groups, OmnichannelReportAccess access, IStringLocalizer S)
        : base(new DataSetDescriptor(Name, S["Campaigns"], S["The campaigns activities belong to."]))
    {
        _campaigns = campaigns;
        _groups = groups;
        _access = access;

        AddField("ItemId", S["Campaign ID"], DataFieldType.Text, record => record.ItemId).Identifier();
        AddField("DisplayText", S["Campaign"], DataFieldType.Text, record => record.DisplayText);
        AddField("Description", S["Description"], DataFieldType.Text, record => record.Description);
        AddField("CampaignGroupId", S["Campaign group ID"], DataFieldType.Text, record => record.CampaignGroupId).Identifier(OmnichannelReportDataSource.Own(CampaignGroupsDataSet.Name));
        AddField("CampaignGroup", S["Campaign group"], DataFieldType.Text, record => record.CampaignGroupId is not null && _groupNames.TryGetValue(record.CampaignGroupId, out var group) ? group : null);
        AddField("InteractionType", S["Interaction type"], DataFieldType.Text, record => record.InteractionType);
        AddField("Channel", S["Channel"], DataFieldType.Text, record => record.Channel);
        AddField("CreatedUtc", S["Created"], DataFieldType.DateTime, record => record.CreatedUtc);
        AddField("ModifiedUtc", S["Modified"], DataFieldType.DateTime, record => record.ModifiedUtc);
    }

    public override Task<bool> CanReadAsync(DataSourceContext context, CancellationToken cancellationToken = default)
    {
        return _access.CanReadAsync(context);
    }

    protected override async IAsyncEnumerable<OmnichannelCampaign> LoadAsync(DataSourceQuery query, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        _groupNames = (await _groups.GetAllAsync(cancellationToken))
            .Where(group => !string.IsNullOrEmpty(group.ItemId))
            .ToDictionary(group => group.ItemId, group => group.DisplayText, StringComparer.Ordinal);

        foreach (var campaign in (await _campaigns.GetAllAsync(cancellationToken)).OrderByDescending(campaign => campaign.CreatedUtc))
        {
            yield return campaign;
        }
    }
}

/// <summary>
/// One row per campaign group.
/// </summary>
internal sealed class CampaignGroupsDataSet : RecordDataSet<OmnichannelCampaignGroup>
{
    public const string Name = "CampaignGroups";

    private readonly ICatalogManager<OmnichannelCampaignGroup> _groups;
    private readonly OmnichannelReportAccess _access;

    public CampaignGroupsDataSet(ICatalogManager<OmnichannelCampaignGroup> groups, OmnichannelReportAccess access, IStringLocalizer S)
        : base(new DataSetDescriptor(Name, S["Campaign groups"], S["The groups campaigns are organized in."]))
    {
        _groups = groups;
        _access = access;

        AddField("ItemId", S["Campaign group ID"], DataFieldType.Text, record => record.ItemId).Identifier();
        AddField("DisplayText", S["Campaign group"], DataFieldType.Text, record => record.DisplayText);
        AddField("Description", S["Description"], DataFieldType.Text, record => record.Description);
        AddField("CreatedUtc", S["Created"], DataFieldType.DateTime, record => record.CreatedUtc);
    }

    public override Task<bool> CanReadAsync(DataSourceContext context, CancellationToken cancellationToken = default)
    {
        return _access.CanReadAsync(context);
    }

    protected override async IAsyncEnumerable<OmnichannelCampaignGroup> LoadAsync(DataSourceQuery query, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var group in (await _groups.GetAllAsync(cancellationToken)).OrderBy(group => group.DisplayText, StringComparer.CurrentCultureIgnoreCase))
        {
            yield return group;
        }
    }
}

/// <summary>
/// One row per activity batch: a bulk load of activities, such as a dialer list, with its load counts.
/// </summary>
internal sealed class ActivityBatchesDataSet : RecordDataSet<OmnichannelActivityBatch>
{
    public const string Name = "ActivityBatches";

    private readonly ISession _session;
    private readonly OmnichannelReportAccess _access;

    public ActivityBatchesDataSet(ISession session, OmnichannelReportAccess access, IStringLocalizer S)
        : base(new DataSetDescriptor(Name, S["Activity batches"], S["Bulk loads of activities, such as dialer lists, with how many records were loaded and skipped."]) { DefaultDateField = "CreatedUtc" })
    {
        _session = session;
        _access = access;

        var counts = S["Counts"].Value;

        AddField("ItemId", S["Batch ID"], DataFieldType.Text, record => record.ItemId).Identifier();
        AddField("DisplayText", S["Batch"], DataFieldType.Text, record => record.DisplayText);
        AddField("Status", S["Status"], DataFieldType.Text, record => record.Status);
        AddField("Source", S["Source"], DataFieldType.Text, record => record.Source);
        AddField("Channel", S["Channel"], DataFieldType.Text, record => record.Channel);
        AddField("CampaignId", S["Campaign ID"], DataFieldType.Text, record => record.CampaignId).Identifier(OmnichannelReportDataSource.Own(CampaignsDataSet.Name));
        AddField("DialerProfileId", S["Dialer profile ID"], DataFieldType.Text, record => record.DialerProfileId).Identifier(new DataFieldReference("ContactCenter", "DialerProfiles", "ItemId"));
        AddField("UrgencyLevel", S["Urgency"], DataFieldType.Text, record => record.UrgencyLevel);
        AddField("ScheduleAt", S["Scheduled"], DataFieldType.DateTime, record => record.ScheduleAt);
        AddField("CreatedUtc", S["Created"], DataFieldType.DateTime, record => record.CreatedUtc);
        AddField("OwnerId", S["Owner (user ID)"], DataFieldType.Text, record => record.OwnerId).Identifier(OmnichannelReportDataSource.Users());
        AddField("TotalLoaded", S["Loaded"], DataFieldType.Integer, record => record.TotalLoaded, counts);
        AddField("TotalMatched", S["Matched"], DataFieldType.Integer, record => record.TotalMatched, counts);
        AddField("TotalSkippedAsDuplicate", S["Skipped as duplicates"], DataFieldType.Integer, record => record.TotalSkippedAsDuplicate, counts);
        AddField("TotalSkippedAsOptedOut", S["Skipped as opted out"], DataFieldType.Integer, record => record.TotalSkippedAsOptedOut, counts);
        AddField("TotalSkippedForNoDestination", S["Skipped without a destination"], DataFieldType.Integer, record => record.TotalSkippedForNoDestination, counts);
        AddField("TotalSkippedAsNotInService", S["Skipped as not in service"], DataFieldType.Integer, record => record.TotalSkippedAsNotInService, counts);
        AddField("TotalSkippedByLimit", S["Skipped by the limit"], DataFieldType.Integer, record => record.TotalSkippedByLimit, counts);
    }

    public override Task<bool> CanReadAsync(DataSourceContext context, CancellationToken cancellationToken = default)
    {
        return _access.CanReadAsync(context);
    }

    protected override IAsyncEnumerable<OmnichannelActivityBatch> LoadAsync(DataSourceQuery query, CancellationToken cancellationToken)
    {
        var (from, to) = DataConditions.DateRange(query.Conditions, "CreatedUtc");

        IQueryIndex<OmnichannelActivityBatchIndex> Filter(IQueryIndex<OmnichannelActivityBatchIndex> records)
        {
            if (from.HasValue)
            {
                var value = from.Value;
                records = records.Where(index => index.CreatedUtc >= value);
            }

            if (to.HasValue)
            {
                var value = to.Value;
                records = records.Where(index => index.CreatedUtc <= value);
            }

            return records;
        }

        return ReportIndexPages.ReadAsync<OmnichannelActivityBatch, OmnichannelActivityBatchIndex>(
            _session,
            Filter,
            index => index.CreatedUtc,
            OmnichannelConstants.CollectionName,
            cancellationToken);
    }
}
