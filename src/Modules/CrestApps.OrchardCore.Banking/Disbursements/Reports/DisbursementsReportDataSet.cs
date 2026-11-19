using System.Linq.Expressions;
using CrestApps.OrchardCore.Banking.Disbursements.Core.Indexes;
using CrestApps.OrchardCore.Banking.Disbursements.Models;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using CrestApps.OrchardCore.SourceAccess;
using CrestApps.OrchardCore.YesSql.Core.SourceAccess;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Banking.Disbursements.Reports;

/// <summary>
/// One row per disbursement: payee, amount, method, status, source and dates. It never holds an account number, and
/// reads only the disbursements of the sources the reader may see.
/// </summary>
public sealed class DisbursementsReportDataSet : IndexedReportDataSet<Disbursement, DisbursementIndex>, IDisbursementsReportDataSet
{
    /// <summary>
    /// The data set's technical name.
    /// </summary>
    public const string DataSetName = "Disbursements";

    private readonly ISourceAccessService _access;

    /// <summary>
    /// Initializes a new instance of the <see cref="DisbursementsReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="access">Who may see the disbursements of each source.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DisbursementsReportDataSet(ISession session, IAuthorizationService authorizationService, ISourceAccessService access, IStringLocalizer<DisbursementsReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(DataSetName, stringLocalizer["Disbursements"], stringLocalizer["One row per disbursement: payee, amount, method, status, the record it pays and its dates."]),
            session,
            authorizationService,
            DisbursementsConstants.CollectionName,
            DisbursementsPermissions.ViewDisbursements)
    {
        _access = access;

        var S = stringLocalizer;
        var group = S["Disbursement"].Value;
        var dates = S["Dates"].Value;

        AddField("ItemId", S["ID"], DataFieldType.Text, record => record.ItemId, group).Identifier();
        AddField("PayeeName", S["Payee"], DataFieldType.Text, record => record.PayeeName, group);
        AddField("IsBusinessPayee", S["Business payee"], DataFieldType.Boolean, record => record.IsBusinessPayee, group);
        AddField("Amount", S["Amount"], DataFieldType.Decimal, record => record.Amount, group);
        AddField("Currency", S["Currency"], DataFieldType.Text, record => record.Currency, group);
        AddField("Method", S["Method"], DataFieldType.Text, record => record.Method, group);
        AddField("Provider", S["Provider"], DataFieldType.Text, record => record.Provider, group);
        AddField("Status", S["Status"], DataFieldType.Text, record => record.Status.ToString(), group).Description = S["Pending, Batched, Submitted, Settled, Returned, Failed, Canceled or Reversed."].Value;
        AddField("IsPrenote", S["Test entry"], DataFieldType.Boolean, record => record.IsPrenote, group);
        AddField("SourceBankAccountId", S["From bank account ID"], DataFieldType.Text, record => record.SourceBankAccountId, group);
        AddField("DestinationBankAccountId", S["To bank account ID"], DataFieldType.Text, record => record.DestinationBankAccountId, group);
        AddField("SourceKind", S["Source kind"], DataFieldType.Text, record => record.SourceKind, group);
        AddField("SourceId", S["Source ID"], DataFieldType.Text, record => record.SourceId, group);
        AddField("BatchId", S["Batch ID"], DataFieldType.Text, record => record.BatchId, group);
        AddField("TraceNumber", S["Trace number"], DataFieldType.Text, record => record.TraceNumber, group);
        AddField("FailureCode", S["Return or failure code"], DataFieldType.Text, record => record.FailureCode, group);
        AddField("Memo", S["Memo"], DataFieldType.Text, record => record.Memo, group);
        AddField("EffectiveDate", S["Arrives"], DataFieldType.Date, record => record.EffectiveDate?.ToDateTime(TimeOnly.MinValue), dates);
        AddField("CreatedUtc", S["Recorded"], DataFieldType.DateTime, record => record.CreatedUtc, dates);
        AddField("SubmittedUtc", S["Submitted"], DataFieldType.DateTime, record => record.SubmittedUtc, dates);
        AddField("ClosedUtc", S["Settled or closed"], DataFieldType.DateTime, record => record.ClosedUtc, dates);
        AddField("ModifiedUtc", S["Changed"], DataFieldType.DateTime, record => record.ModifiedUtc ?? record.CreatedUtc, dates).Description = S["When the disbursement last changed: what a pipeline reads new rows by."].Value;

        CompleteDescription();
    }

    /// <inheritdoc />
    protected override (string Field, Expression<Func<DisbursementIndex, DateTime>> Column)? DateColumn
        => ("ModifiedUtc", index => index.ModifiedUtc);

    /// <inheritdoc />
    protected override IReadOnlyDictionary<string, Expression<Func<DisbursementIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<DisbursementIndex, string>>>(StringComparer.Ordinal)
        {
            ["ItemId"] = index => index.ItemId,
            ["SourceId"] = index => index.SourceId,
            ["BatchId"] = index => index.BatchId,
        };

    /// <inheritdoc />
    protected override void DescribeColumns(ReportIndexConditions<DisbursementIndex> columns)
    {
        columns.Text("Status", nameof(DisbursementIndex.Status));
        columns.Text("Method", nameof(DisbursementIndex.Method));
    }

    /// <inheritdoc />
    protected override async Task<Func<IQueryIndex<DisbursementIndex>, IQueryIndex<DisbursementIndex>>> RestrictAsync(DataSourceContext context, CancellationToken cancellationToken)
    {
        var filter = await _access.GetFilterAsync(context?.User, SourceAccessLevel.View, cancellationToken);

        if (SourceAccessExpressions.Restrict<DisbursementIndex>(filter) is not { } restrict)
        {
            return rows => rows;
        }

        return rows => rows.Where(restrict);
    }
}
