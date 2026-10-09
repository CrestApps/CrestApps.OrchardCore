using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using YesSql;
using YesSql.Services;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Reports.DataSources;

/// <summary>
/// Exposes Omnichannel records to the report builder: activities (including the dialer's work), dispositions,
/// campaigns, campaign groups, and activity batches. Every data set requires the View Omnichannel reports permission.
/// </summary>
public sealed class OmnichannelReportDataSource : ReportRecordDataSource
{
    /// <summary>
    /// The technical name of the data source.
    /// </summary>
    public const string SourceName = "Omnichannel";

    private readonly IReportRecordDataSet[] _dataSets;
    private readonly IStringLocalizer S;

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
        S = stringLocalizer;

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
    public override string Name => SourceName;

    /// <inheritdoc/>
    public override LocalizedString DisplayName => S["Omnichannel"];

    /// <inheritdoc/>
    public override LocalizedString Description => S["Activities, including the dialer's calls, with their dispositions, campaigns, and batches."];

    /// <inheritdoc/>
    protected override IEnumerable<IReportRecordDataSet> DataSets => _dataSets;

    internal static ReportFieldReference Users()
    {
        return new ReportFieldReference(ReportsConstants.UsersDataSource, ReportsConstants.UsersDataSet, ReportsConstants.UserIdField);
    }

    internal static ReportFieldReference Own(string dataSet)
    {
        return new ReportFieldReference(SourceName, dataSet, "ItemId");
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

    public async Task<bool> CanReadAsync(ReportDataSourceContext context)
    {
        return context?.User is not null &&
            await _authorizationService.AuthorizeAsync(context.User, OmnichannelConstants.Permissions.ViewReports);
    }
}

/// <summary>
/// One row per activity: a task, call, message, or any other unit of work, including the dialer's attempts.
/// </summary>
internal sealed class ActivitiesDataSet : ReportRecordDataSet<OmnichannelActivity>
{
    public const string Name = "Activities";

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
        : base(new ReportDataSetDescriptor(Name, S["Activities"], S["One row per activity: tasks, calls, messages, and the dialer's attempts."]) { DefaultDateField = "CreatedUtc" })
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

        AddField("ItemId", S["Activity ID"], ReportDataType.Text, record => record.ItemId, activity, isIdentifier: true);
        AddField("Kind", S["Kind"], ReportDataType.Text, record => record.Kind, activity);
        AddField("Source", S["Source"], ReportDataType.Text, record => record.Source, activity);
        AddField("Channel", S["Channel"], ReportDataType.Text, record => record.Channel, activity);
        AddField("InteractionType", S["Interaction type"], ReportDataType.Text, record => record.InteractionType, activity);
        AddField("Status", S["Status"], ReportDataType.Text, record => record.Status, activity);
        AddField("UrgencyLevel", S["Urgency"], ReportDataType.Text, record => record.UrgencyLevel, activity);
        AddField("Attempts", S["Attempts"], ReportDataType.Integer, record => record.Attempts, activity);
        AddField("ProcessingAttempts", S["Processing attempts"], ReportDataType.Integer, record => record.ProcessingAttempts, activity);
        AddField("PreferredDestination", S["Destination"], ReportDataType.Text, record => record.PreferredDestination, activity);
        AddField("SubjectContentType", S["Subject type"], ReportDataType.Text, record => record.SubjectContentType, activity);
        AddField("AiEscalated", S["Escalated by AI"], ReportDataType.Boolean, record => record.AiEscalated, activity);

        AddField("AssignmentStatus", S["Assignment status"], ReportDataType.Text, record => record.AssignmentStatus, people);
        AddField("AssignedToId", S["Assigned to (user ID)"], ReportDataType.Text, record => record.AssignedToId, people, isIdentifier: true, OmnichannelReportDataSource.Users());
        AddField("AssignedToUsername", S["Assigned to"], ReportDataType.Text, record => record.AssignedToUsername, people);
        AddField("CreatedById", S["Created by (user ID)"], ReportDataType.Text, record => record.CreatedById, people, isIdentifier: true, OmnichannelReportDataSource.Users());
        AddField("CreatedByUsername", S["Created by"], ReportDataType.Text, record => record.CreatedByUsername, people);
        AddField("CompletedById", S["Completed by (user ID)"], ReportDataType.Text, record => record.CompletedById, people, isIdentifier: true, OmnichannelReportDataSource.Users());
        AddField("CompletedByUsername", S["Completed by"], ReportDataType.Text, record => record.CompletedByUsername, people);

        AddField("DispositionId", S["Disposition ID"], ReportDataType.Text, record => record.DispositionId, outcome, isIdentifier: true, OmnichannelReportDataSource.Own(DispositionsDataSet.Name));
        AddField("Disposition", S["Disposition"], ReportDataType.Text, record => Lookup(Dispositions, record.DispositionId), outcome);
        AddField("DispositionedBy", S["Dispositioned by"], ReportDataType.Text, record => record.DispositionedBy, outcome);
        AddField("TerminalReasonCode", S["Terminal reason"], ReportDataType.Text, record => record.TerminalReasonCode, outcome);

        AddField("CreatedUtc", S["Created"], ReportDataType.DateTime, record => record.CreatedUtc, dates);
        AddField("ScheduledUtc", S["Scheduled"], ReportDataType.DateTime, record => record.ScheduledUtc, dates);
        AddField("AssignedToUtc", S["Assigned"], ReportDataType.DateTime, record => record.AssignedToUtc, dates);
        AddField("CompletedUtc", S["Completed"], ReportDataType.DateTime, record => record.CompletedUtc, dates);
        AddField("HandleMinutes", S["Minutes to complete"], ReportDataType.Decimal, record => record.CompletedUtc.HasValue && record.CompletedUtc.Value >= record.CreatedUtc
            ? Math.Round((decimal)(record.CompletedUtc.Value - record.CreatedUtc).TotalMinutes, 2)
            : null, dates);

        AddField("CampaignId", S["Campaign ID"], ReportDataType.Text, record => record.CampaignId, links, isIdentifier: true, OmnichannelReportDataSource.Own(CampaignsDataSet.Name));
        AddField("Campaign", S["Campaign"], ReportDataType.Text, record => Lookup(Campaigns, record.CampaignId), links);
        AddField("ContactContentItemId", S["Contact ID"], ReportDataType.Text, record => record.ContactContentItemId, links, isIdentifier: true);
        AddField("ContactContentType", S["Contact type"], ReportDataType.Text, record => record.ContactContentType, links);

        // The dialer and chat data sets live in other modules; these references join to them when they are enabled.
        AddField("DialerProfileId", S["Dialer profile ID"], ReportDataType.Text, record => record.DialerProfileId, links, isIdentifier: true, new ReportFieldReference("ContactCenter", "DialerProfiles", "ItemId"));
        AddField("AISessionId", S["AI chat session ID"], ReportDataType.Text, record => record.AISessionId, links, isIdentifier: true, new ReportFieldReference("AIChat", "ChatSessions", "SessionId"));
        AddField("AIProfileId", S["AI profile ID"], ReportDataType.Text, record => record.AIProfileId, links, isIdentifier: true);
        AddField("CadenceId", S["Cadence ID"], ReportDataType.Text, record => record.CadenceId, links, isIdentifier: true);
    }

    // The names of the dispositions and campaigns, read once per query.
    private Dictionary<string, string> Dispositions { get; set; } = [];

    private Dictionary<string, string> Campaigns { get; set; } = [];

    public override Task<bool> CanReadAsync(ReportDataSourceContext context)
    {
        return _access.CanReadAsync(context);
    }

    protected override async Task<IEnumerable<OmnichannelActivity>> LoadAsync(ReportDataSourceQuery query, int take, CancellationToken cancellationToken)
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

        var records = _session.Query<OmnichannelActivity, OmnichannelActivityIndex>(collection: OmnichannelConstants.CollectionName);
        var (createdFrom, createdTo) = ReportDateRange.For(query.Conditions, "CreatedUtc");
        var (scheduledFrom, scheduledTo) = ReportDateRange.For(query.Conditions, "ScheduledUtc");
        var (completedFrom, completedTo) = ReportDateRange.For(query.Conditions, "CompletedUtc");

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

        return await records
            .OrderByDescending(index => index.CreatedUtc)
            .ThenByDescending(index => index.DocumentId)
            .Take(take)
            .ListAsync(cancellationToken);
    }

    private static string Lookup(Dictionary<string, string> names, string id)
    {
        return id is not null && names.TryGetValue(id, out var name) ? name : null;
    }
}

/// <summary>
/// One row per disposition. Dispositions are grouped by their outcome.
/// </summary>
internal sealed class DispositionsDataSet : ReportRecordDataSet<OmnichannelDisposition>
{
    public const string Name = "Dispositions";

    private readonly INamedCatalogManager<OmnichannelDisposition> _dispositions;
    private readonly OmnichannelReportAccess _access;

    public DispositionsDataSet(INamedCatalogManager<OmnichannelDisposition> dispositions, OmnichannelReportAccess access, IStringLocalizer S)
        : base(new ReportDataSetDescriptor(Name, S["Dispositions"], S["The dispositions activities are closed with, grouped by outcome."]))
    {
        _dispositions = dispositions;
        _access = access;

        AddField("ItemId", S["Disposition ID"], ReportDataType.Text, record => record.ItemId, isIdentifier: true);
        AddField("Name", S["Name"], ReportDataType.Text, record => record.Name);
        AddField("Description", S["Description"], ReportDataType.Text, record => record.Description);
        AddField("Outcome", S["Outcome"], ReportDataType.Text, record => record.Outcome);
        AddField("CaptureDate", S["Asks for a date"], ReportDataType.Boolean, record => record.CaptureDate);
        AddField("CreatedUtc", S["Created"], ReportDataType.DateTime, record => record.CreatedUtc);
        AddField("ModifiedUtc", S["Modified"], ReportDataType.DateTime, record => record.ModifiedUtc);
    }

    public override Task<bool> CanReadAsync(ReportDataSourceContext context)
    {
        return _access.CanReadAsync(context);
    }

    protected override async Task<IEnumerable<OmnichannelDisposition>> LoadAsync(ReportDataSourceQuery query, int take, CancellationToken cancellationToken)
    {
        return (await _dispositions.GetAllAsync(cancellationToken))
            .OrderBy(disposition => disposition.Name, StringComparer.CurrentCultureIgnoreCase)
            .Take(take);
    }
}

/// <summary>
/// One row per campaign.
/// </summary>
internal sealed class CampaignsDataSet : ReportRecordDataSet<OmnichannelCampaign>
{
    public const string Name = "Campaigns";

    private readonly ICatalogManager<OmnichannelCampaign> _campaigns;
    private readonly ICatalogManager<OmnichannelCampaignGroup> _groups;
    private readonly OmnichannelReportAccess _access;
    private Dictionary<string, string> _groupNames = [];

    public CampaignsDataSet(ICatalogManager<OmnichannelCampaign> campaigns, ICatalogManager<OmnichannelCampaignGroup> groups, OmnichannelReportAccess access, IStringLocalizer S)
        : base(new ReportDataSetDescriptor(Name, S["Campaigns"], S["The campaigns activities belong to."]))
    {
        _campaigns = campaigns;
        _groups = groups;
        _access = access;

        AddField("ItemId", S["Campaign ID"], ReportDataType.Text, record => record.ItemId, isIdentifier: true);
        AddField("DisplayText", S["Campaign"], ReportDataType.Text, record => record.DisplayText);
        AddField("Description", S["Description"], ReportDataType.Text, record => record.Description);
        AddField("CampaignGroupId", S["Campaign group ID"], ReportDataType.Text, record => record.CampaignGroupId, isIdentifier: true, references: OmnichannelReportDataSource.Own(CampaignGroupsDataSet.Name));
        AddField("CampaignGroup", S["Campaign group"], ReportDataType.Text, record => record.CampaignGroupId is not null && _groupNames.TryGetValue(record.CampaignGroupId, out var group) ? group : null);
        AddField("InteractionType", S["Interaction type"], ReportDataType.Text, record => record.InteractionType);
        AddField("Channel", S["Channel"], ReportDataType.Text, record => record.Channel);
        AddField("CreatedUtc", S["Created"], ReportDataType.DateTime, record => record.CreatedUtc);
        AddField("ModifiedUtc", S["Modified"], ReportDataType.DateTime, record => record.ModifiedUtc);
    }

    public override Task<bool> CanReadAsync(ReportDataSourceContext context)
    {
        return _access.CanReadAsync(context);
    }

    protected override async Task<IEnumerable<OmnichannelCampaign>> LoadAsync(ReportDataSourceQuery query, int take, CancellationToken cancellationToken)
    {
        _groupNames = (await _groups.GetAllAsync(cancellationToken))
            .Where(group => !string.IsNullOrEmpty(group.ItemId))
            .ToDictionary(group => group.ItemId, group => group.DisplayText, StringComparer.Ordinal);

        return (await _campaigns.GetAllAsync(cancellationToken))
            .OrderByDescending(campaign => campaign.CreatedUtc)
            .Take(take);
    }
}

/// <summary>
/// One row per campaign group.
/// </summary>
internal sealed class CampaignGroupsDataSet : ReportRecordDataSet<OmnichannelCampaignGroup>
{
    public const string Name = "CampaignGroups";

    private readonly ICatalogManager<OmnichannelCampaignGroup> _groups;
    private readonly OmnichannelReportAccess _access;

    public CampaignGroupsDataSet(ICatalogManager<OmnichannelCampaignGroup> groups, OmnichannelReportAccess access, IStringLocalizer S)
        : base(new ReportDataSetDescriptor(Name, S["Campaign groups"], S["The groups campaigns are organized in."]))
    {
        _groups = groups;
        _access = access;

        AddField("ItemId", S["Campaign group ID"], ReportDataType.Text, record => record.ItemId, isIdentifier: true);
        AddField("DisplayText", S["Campaign group"], ReportDataType.Text, record => record.DisplayText);
        AddField("Description", S["Description"], ReportDataType.Text, record => record.Description);
        AddField("CreatedUtc", S["Created"], ReportDataType.DateTime, record => record.CreatedUtc);
    }

    public override Task<bool> CanReadAsync(ReportDataSourceContext context)
    {
        return _access.CanReadAsync(context);
    }

    protected override async Task<IEnumerable<OmnichannelCampaignGroup>> LoadAsync(ReportDataSourceQuery query, int take, CancellationToken cancellationToken)
    {
        return (await _groups.GetAllAsync(cancellationToken))
            .OrderBy(group => group.DisplayText, StringComparer.CurrentCultureIgnoreCase)
            .Take(take);
    }
}

/// <summary>
/// One row per activity batch: a bulk load of activities, such as a dialer list, with its load counts.
/// </summary>
internal sealed class ActivityBatchesDataSet : ReportRecordDataSet<OmnichannelActivityBatch>
{
    public const string Name = "ActivityBatches";

    private readonly ISession _session;
    private readonly OmnichannelReportAccess _access;

    public ActivityBatchesDataSet(ISession session, OmnichannelReportAccess access, IStringLocalizer S)
        : base(new ReportDataSetDescriptor(Name, S["Activity batches"], S["Bulk loads of activities, such as dialer lists, with how many records were loaded and skipped."]) { DefaultDateField = "CreatedUtc" })
    {
        _session = session;
        _access = access;

        var counts = S["Counts"].Value;

        AddField("ItemId", S["Batch ID"], ReportDataType.Text, record => record.ItemId, isIdentifier: true);
        AddField("DisplayText", S["Batch"], ReportDataType.Text, record => record.DisplayText);
        AddField("Status", S["Status"], ReportDataType.Text, record => record.Status);
        AddField("Source", S["Source"], ReportDataType.Text, record => record.Source);
        AddField("Channel", S["Channel"], ReportDataType.Text, record => record.Channel);
        AddField("CampaignId", S["Campaign ID"], ReportDataType.Text, record => record.CampaignId, isIdentifier: true, references: OmnichannelReportDataSource.Own(CampaignsDataSet.Name));
        AddField("DialerProfileId", S["Dialer profile ID"], ReportDataType.Text, record => record.DialerProfileId, isIdentifier: true, references: new ReportFieldReference("ContactCenter", "DialerProfiles", "ItemId"));
        AddField("UrgencyLevel", S["Urgency"], ReportDataType.Text, record => record.UrgencyLevel);
        AddField("ScheduleAt", S["Scheduled"], ReportDataType.DateTime, record => record.ScheduleAt);
        AddField("CreatedUtc", S["Created"], ReportDataType.DateTime, record => record.CreatedUtc);
        AddField("OwnerId", S["Owner (user ID)"], ReportDataType.Text, record => record.OwnerId, isIdentifier: true, references: OmnichannelReportDataSource.Users());
        AddField("TotalLoaded", S["Loaded"], ReportDataType.Integer, record => record.TotalLoaded, counts);
        AddField("TotalMatched", S["Matched"], ReportDataType.Integer, record => record.TotalMatched, counts);
        AddField("TotalSkippedAsDuplicate", S["Skipped as duplicates"], ReportDataType.Integer, record => record.TotalSkippedAsDuplicate, counts);
        AddField("TotalSkippedAsOptedOut", S["Skipped as opted out"], ReportDataType.Integer, record => record.TotalSkippedAsOptedOut, counts);
        AddField("TotalSkippedForNoDestination", S["Skipped without a destination"], ReportDataType.Integer, record => record.TotalSkippedForNoDestination, counts);
        AddField("TotalSkippedAsNotInService", S["Skipped as not in service"], ReportDataType.Integer, record => record.TotalSkippedAsNotInService, counts);
        AddField("TotalSkippedByLimit", S["Skipped by the limit"], ReportDataType.Integer, record => record.TotalSkippedByLimit, counts);
    }

    public override Task<bool> CanReadAsync(ReportDataSourceContext context)
    {
        return _access.CanReadAsync(context);
    }

    protected override async Task<IEnumerable<OmnichannelActivityBatch>> LoadAsync(ReportDataSourceQuery query, int take, CancellationToken cancellationToken)
    {
        var records = _session.Query<OmnichannelActivityBatch, OmnichannelActivityBatchIndex>(collection: OmnichannelConstants.CollectionName);
        var (from, to) = ReportDateRange.For(query.Conditions, "CreatedUtc");

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

        return await records
            .OrderByDescending(index => index.CreatedUtc)
            .Take(take)
            .ListAsync(cancellationToken);
    }
}
