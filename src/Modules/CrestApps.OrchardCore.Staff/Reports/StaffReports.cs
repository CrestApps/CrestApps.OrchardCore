using System.Linq.Expressions;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using CrestApps.OrchardCore.Staff.Core.Indexes;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Teams.Core.Indexes;
using CrestApps.OrchardCore.Teams;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using OrchardCore.Localization;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Staff.Reports;

/// <summary>
/// A data set of the Staff report data source.
/// </summary>
public interface IStaffReportDataSet : IRecordDataSet
{
}

/// <summary>
/// The Staff report data source: the people who work for the business and the changes to their employment, for
/// headcount, hires, departures and turnover by team or manager.
/// </summary>
public sealed class StaffReportDataSource : RecordDataSource, IDataSource
{
    private readonly IStaffReportDataSet[] _dataSets;

    /// <summary>
    /// Initializes a new instance of the <see cref="StaffReportDataSource"/> class.
    /// </summary>
    /// <param name="dataSets">The data sets.</param>
    public StaffReportDataSource(IEnumerable<IStaffReportDataSet> dataSets)
    {
        _dataSets = dataSets.ToArray();
    }

    /// <summary>
    /// Gets the data source's technical name.
    /// </summary>
    public static string Name => "Staff";

    /// <summary>
    /// Gets the data source's name.
    /// </summary>
    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<StaffReportDataSource>("Staff");

    /// <summary>
    /// Gets what the data source holds.
    /// </summary>
    public static LocalizationSource Description { get; } = LocalizationSource.Create<StaffReportDataSource>("The people who work for the business, and every change to their job, status, manager and teams.");

    /// <inheritdoc />
    protected override IEnumerable<IRecordDataSet> DataSets => _dataSets;
}

/// <summary>
/// One row per staff member: job, main team, manager, employment, status, capabilities and dates.
/// </summary>
public sealed class StaffMembersReportDataSet : IndexedReportDataSet<StaffMember, StaffMemberIndex>, IStaffReportDataSet
{
    /// <summary>
    /// The data set's technical name.
    /// </summary>
    public const string DataSetName = "StaffMembers";

    private Dictionary<string, string> _teams = new(StringComparer.Ordinal);
    private Dictionary<string, string> _people = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="StaffMembersReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public StaffMembersReportDataSet(ISession session, IAuthorizationService authorizationService, IStringLocalizer<StaffMembersReportDataSet> stringLocalizer)
        : base(new DataSetDescriptor(DataSetName, stringLocalizer["Staff members"], stringLocalizer["One row per staff member: job title, main team, manager, employment, status, capabilities, and when they were hired and left."]), session, authorizationService, StaffConstants.CollectionName, StaffPermissions.ViewStaff)
    {
        var S = stringLocalizer;
        var group = S["Staff"].Value;

        AddField("ItemId", S["ID"], DataFieldType.Text, record => record.ItemId, group).Identifier();
        AddField("Name", S["Name"], DataFieldType.Text, record => record.DisplayName, group);
        AddField("EmployeeNumber", S["Employee number"], DataFieldType.Text, record => record.EmployeeNumber, group);
        AddField("JobTitle", S["Job title"], DataFieldType.Text, record => record.JobTitle, group);
        AddField("TeamName", S["Main team"], DataFieldType.Text, record => _teams.GetValueOrDefault(record.PrimaryTeamId ?? string.Empty), group);
        AddField("ManagerName", S["Manager"], DataFieldType.Text, record => _people.GetValueOrDefault(record.ManagerId ?? string.Empty), group);
        AddField("EmploymentType", S["Employment"], DataFieldType.Text, record => record.EmploymentType.ToString(), group);
        AddField("Status", S["Status"], DataFieldType.Text, record => record.Status.ToString(), group);
        AddField("HasAccount", S["Has an account"], DataFieldType.Boolean, record => !string.IsNullOrEmpty(record.UserId), group);
        AddField("Capabilities", S["Capabilities"], DataFieldType.Text, record => string.Join(", ", record.Capabilities), group);
        AddField("WorkLocation", S["Work location"], DataFieldType.Text, record => record.WorkLocation, group);
        AddField("ContractedHoursPerWeek", S["Contracted hours a week"], DataFieldType.Decimal, record => record.ContractedHoursPerWeek, group);
        AddField("HireDate", S["Hired"], DataFieldType.Date, record => record.HireDate?.ToDateTime(TimeOnly.MinValue), group);
        AddField("TerminationDate", S["Left"], DataFieldType.Date, record => record.TerminationDate?.ToDateTime(TimeOnly.MinValue), group);
        AddField("CreatedUtc", S["Added"], DataFieldType.DateTime, record => record.CreatedUtc, group);

        CompleteDescription();
    }

    /// <inheritdoc />
    protected override IReadOnlyDictionary<string, Expression<Func<StaffMemberIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<StaffMemberIndex, string>>>(StringComparer.Ordinal)
        {
            ["ItemId"] = index => index.ItemId,
        };

    /// <summary>
    /// Reads the team and staff names once per run.
    /// </summary>
    /// <param name="context">The context of the run.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>No narrowing.</returns>
    protected override async Task<Func<IQueryIndex<StaffMemberIndex>, IQueryIndex<StaffMemberIndex>>> RestrictAsync(DataSourceContext context, CancellationToken cancellationToken)
    {
        _teams = (await Session.QueryIndex<TeamIndex>(collection: TeamsConstants.CollectionName).ListAsync(cancellationToken))
            .GroupBy(index => index.ItemId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Name, StringComparer.Ordinal);
        _people = (await Session.QueryIndex<StaffMemberIndex>(collection: StaffConstants.CollectionName).ListAsync(cancellationToken))
            .GroupBy(index => index.ItemId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().DisplayName, StringComparer.Ordinal);

        return await base.RestrictAsync(context, cancellationToken);
    }

}

/// <summary>
/// One row per change to a staff member's employment: hires, job, status, manager and team changes, and departures.
/// </summary>
public sealed class StaffHistoryReportDataSet : IndexedReportDataSet<StaffHistoryEntry, StaffHistoryIndex>, IStaffReportDataSet
{
    /// <summary>
    /// The data set's technical name.
    /// </summary>
    public const string DataSetName = "StaffHistory";

    private Dictionary<string, string> _people = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="StaffHistoryReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public StaffHistoryReportDataSet(ISession session, IAuthorizationService authorizationService, IStringLocalizer<StaffHistoryReportDataSet> stringLocalizer)
        : base(new DataSetDescriptor(DataSetName, stringLocalizer["Staff history"], stringLocalizer["One row per change to a staff member's employment: added, job title, status, employment, manager, teams, work location and last day."]), session, authorizationService, StaffConstants.CollectionName, StaffPermissions.ViewStaff)
    {
        var S = stringLocalizer;
        var group = S["Change"].Value;

        AddField("ItemId", S["ID"], DataFieldType.Text, record => record.ItemId, group).Identifier();
        AddField("StaffMemberId", S["Staff member ID"], DataFieldType.Text, record => record.StaffMemberId, group);
        AddField("StaffName", S["Staff member"], DataFieldType.Text, record => _people.GetValueOrDefault(record.StaffMemberId ?? string.Empty), group);
        AddField("Field", S["What changed"], DataFieldType.Text, record => record.Field, group);
        AddField("From", S["From"], DataFieldType.Text, record => record.From, group);
        AddField("To", S["To"], DataFieldType.Text, record => record.To, group);
        AddField("ChangedUtc", S["When"], DataFieldType.DateTime, record => record.ChangedUtc, group);

        CompleteDescription();
    }

    /// <inheritdoc />
    protected override (string Field, Expression<Func<StaffHistoryIndex, DateTime>> Column)? DateColumn => ("ChangedUtc", index => index.ChangedUtc);

    /// <inheritdoc />
    protected override IReadOnlyDictionary<string, Expression<Func<StaffHistoryIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<StaffHistoryIndex, string>>>(StringComparer.Ordinal)
        {
            ["ItemId"] = index => index.ItemId,
            ["StaffMemberId"] = index => index.StaffMemberId,
            ["Field"] = index => index.Field,
        };

    /// <summary>
    /// Reads the staff names once per run.
    /// </summary>
    /// <param name="context">The context of the run.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>No narrowing.</returns>
    protected override async Task<Func<IQueryIndex<StaffHistoryIndex>, IQueryIndex<StaffHistoryIndex>>> RestrictAsync(DataSourceContext context, CancellationToken cancellationToken)
    {
        _people = (await Session.QueryIndex<StaffMemberIndex>(collection: StaffConstants.CollectionName).ListAsync(cancellationToken))
            .GroupBy(index => index.ItemId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().DisplayName, StringComparer.Ordinal);

        return await base.RestrictAsync(context, cancellationToken);
    }

}
