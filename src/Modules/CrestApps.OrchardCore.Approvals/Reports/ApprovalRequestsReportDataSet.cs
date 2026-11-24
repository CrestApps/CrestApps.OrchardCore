using System.Linq.Expressions;
using CrestApps.OrchardCore.Approvals.Core.Indexes;
using CrestApps.OrchardCore.Approvals.Models;
using CrestApps.OrchardCore.Approvals.Services;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using CrestApps.OrchardCore.SourceAccess;
using CrestApps.OrchardCore.YesSql.Core.SourceAccess;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DataSources;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Approvals.Reports;

/// <summary>
/// One row per approval request: what was asked, the policy, the status, who asked, who decided and the dates. It
/// reads only the requests about subjects the reader may see.
/// </summary>
public sealed class ApprovalRequestsReportDataSet : IndexedReportDataSet<ApprovalRequest, ApprovalRequestIndex>, IApprovalsReportDataSet
{
    /// <summary>
    /// The data set's technical name.
    /// </summary>
    public const string DataSetName = "ApprovalRequests";

    private readonly ISourceAccessService _access;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApprovalRequestsReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="access">Who may see the requests of each subject kind.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ApprovalRequestsReportDataSet(ISession session, IAuthorizationService authorizationService, ISourceAccessService access, IApprovalTypeCatalog types, IStringLocalizerFactory localizerFactory, IStringLocalizer<ApprovalRequestsReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(DataSetName, stringLocalizer["Approval requests"], stringLocalizer["One row per approval request: what was asked, the policy, the status, who asked, who decided and the dates."]),
            session,
            authorizationService,
            ApprovalsConstants.CollectionName,
            ApprovalsPermissions.ViewApprovalRequests)
    {
        _access = access;

        var S = stringLocalizer;
        var group = S["Request"].Value;
        var dates = S["Dates"].Value;

        AddField("ItemId", S["ID"], DataFieldType.Text, record => record.ItemId, group).Identifier();
        AddField("Title", S["Title"], DataFieldType.Text, record => record.Title, group);
        AddField("SubjectKind", S["Subject kind"], DataFieldType.Text, record => record.SubjectKind, group);
        AddField("Type", S["Type"], DataFieldType.Text, record => types.Find(record.SubjectKind)?.DisplayName is { } name ? localizerFactory.Localize(name).Value : record.SubjectKind, group).Description = S["The approval type as people see it, such as Pay run."].Value;
        AddField("Stage", S["Stage"], DataFieldType.Integer, record => record.CurrentStage + 1, group).Description = S["The stage being decided, or the last one decided, from 1."].Value;
        AddField("SubjectId", S["Subject ID"], DataFieldType.Text, record => record.SubjectId, group);
        AddField("PolicyId", S["Policy ID"], DataFieldType.Text, record => record.PolicyId, group);
        AddField("PolicyTitle", S["Policy"], DataFieldType.Text, record => record.PolicyTitle, group);
        AddField("Status", S["Status"], DataFieldType.Text, record => record.Status.ToString(), group).Description = S["Pending, Approved, Rejected or Canceled."].Value;
        AddField("Amount", S["Amount"], DataFieldType.Decimal, record => record.Amount, group);
        AddField("Currency", S["Currency"], DataFieldType.Text, record => record.Currency, group);
        AddField("RequiredApprovals", S["Approvals needed"], DataFieldType.Integer, record => record.RequiredApprovals, group);
        AddField("ApprovalCount", S["Approvals given"], DataFieldType.Integer, record => record.ApprovalCount, group);
        AddField("RequesterName", S["Requested by"], DataFieldType.Text, record => record.RequesterName, group);
        AddField("DecidedBy", S["Decided by"], DataFieldType.Text, record => string.Join(", ", record.Decisions.Select(decision => decision.UserName)), group);
        AddField("RejectionNote", S["Rejection note"], DataFieldType.Text, record => record.Decisions.LastOrDefault(decision => decision.Kind == ApprovalDecisionKind.Reject)?.Note, group);
        AddField("IsHistorical", S["Imported"], DataFieldType.Boolean, record => record.IsHistorical, group);
        AddField("CreatedUtc", S["Requested"], DataFieldType.DateTime, record => record.CreatedUtc, dates);
        AddField("DueUtc", S["Due"], DataFieldType.DateTime, record => record.DueUtc, dates);
        AddField("ClosedUtc", S["Decided"], DataFieldType.DateTime, record => record.ClosedUtc, dates);
        AddField("HoursToDecide", S["Hours to decide"], DataFieldType.Decimal, record => record.ClosedUtc is { } closed ? Math.Round((decimal)(closed - record.CreatedUtc).TotalHours, 2) : null, dates);
        AddField("ModifiedUtc", S["Changed"], DataFieldType.DateTime, record => record.ModifiedUtc ?? record.CreatedUtc, dates).Description = S["When the request last changed: what a pipeline reads new rows by."].Value;

        CompleteDescription();
    }

    /// <inheritdoc />
    protected override (string Field, Expression<Func<ApprovalRequestIndex, DateTime>> Column)? DateColumn
        => ("ModifiedUtc", index => index.ModifiedUtc);

    /// <inheritdoc />
    protected override IReadOnlyDictionary<string, Expression<Func<ApprovalRequestIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<ApprovalRequestIndex, string>>>(StringComparer.Ordinal)
        {
            ["ItemId"] = index => index.ItemId,
            ["SubjectId"] = index => index.SubjectId,
            ["PolicyId"] = index => index.PolicyId,
        };

    /// <inheritdoc />
    protected override void DescribeColumns(ReportIndexConditions<ApprovalRequestIndex> columns)
    {
        columns.Text("Status", nameof(ApprovalRequestIndex.Status));
        columns.Text("SubjectKind", nameof(ApprovalRequestIndex.SourceKind));
    }

    /// <inheritdoc />
    protected override async Task<Func<IQueryIndex<ApprovalRequestIndex>, IQueryIndex<ApprovalRequestIndex>>> RestrictAsync(DataSourceContext context, CancellationToken cancellationToken)
    {
        var filter = await _access.GetFilterAsync(context?.User, SourceAccessLevel.View, cancellationToken);

        if (SourceAccessExpressions.Restrict<ApprovalRequestIndex>(filter) is not { } restrict)
        {
            return rows => rows;
        }

        return rows => rows.Where(restrict);
    }
}
