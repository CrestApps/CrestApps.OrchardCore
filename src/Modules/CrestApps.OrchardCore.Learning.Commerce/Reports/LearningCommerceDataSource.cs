using System.Linq.Expressions;
using CrestApps.OrchardCore.Accounts;
using CrestApps.OrchardCore.Learning.Commerce.Core.Indexes;
using CrestApps.OrchardCore.Learning.Commerce.Models;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using OrchardCore.Localization;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Learning.Commerce.Reports;

public sealed class LearningCommerceDataSource : RecordDataSource, IDataSource
{
    private readonly IRecordDataSet[] _dataSets;

    public LearningCommerceDataSource(LicensePoolReportDataSet pools, LicenseSeatReportDataSet seats)
    {
        _dataSets = [pools, seats];
    }

    public static string Name => LearningCommerceConstants.DataSourceName;


    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<LearningCommerceDataSource>("Learning commerce");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<LearningCommerceDataSource>("The course seats accounts bought and who holds them.");

    protected override IEnumerable<IRecordDataSet> DataSets => _dataSets;
}

public sealed class LicensePoolReportDataSet : IndexedReportDataSet<LicensePool, LicensePoolIndex>
{
    public const string DataSetName = "LearningLicensePools";

    public LicensePoolReportDataSet(ISession session, IAuthorizationService authorizationService, IStringLocalizer<LicensePoolReportDataSet> stringLocalizer)
        : base(new DataSetDescriptor(DataSetName, stringLocalizer["Licence pools"], stringLocalizer["One row per pool of course seats an account bought."]), session, authorizationService, LearningCommerceConstants.CollectionName, LearningPermissions.ViewLearningReports)
    {
        var S = stringLocalizer;
        var group = S["Licence pool"].Value;

        AddField("PoolId", S["Pool ID"], DataFieldType.Text, record => record.ItemId, group).Identifier();
        AddField("AccountId", S["Account"], DataFieldType.Text, record => record.AccountId, group).Identifier(ReportDataReferences.ContentItem(AccountsConstants.ContentTypes.Account));
        AddField("CourseId", S["Course"], DataFieldType.Text, record => record.CourseId, group).Identifier(ReportDataReferences.ContentItem(LearningConstants.ContentTypes.Course));
        AddField("Title", S["Title"], DataFieldType.Text, record => record.Title, group);
        AddField("Seats", S["Seats bought"], DataFieldType.Integer, record => record.Seats, group);
        AddField("State", S["State"], DataFieldType.Text, record => record.State.ToString(), group);
        AddField("OrderId", S["Order"], DataFieldType.Text, record => record.OrderId, group).Identifier();
        AddField("CreatedUtc", S["Bought"], DataFieldType.DateTime, record => record.CreatedUtc, group);

        CompleteDescription();
    }

    protected override IReadOnlyDictionary<string, Expression<Func<LicensePoolIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<LicensePoolIndex, string>>>(StringComparer.Ordinal)
        {
            ["PoolId"] = index => index.ItemId,
            ["AccountId"] = index => index.AccountId,
            ["CourseId"] = index => index.CourseId,
        };
}

public sealed class LicenseSeatReportDataSet : IndexedReportDataSet<LicenseSeat, LicenseSeatIndex>
{
    public const string DataSetName = "LearningSeatHolders";

    public LicenseSeatReportDataSet(ISession session, IAuthorizationService authorizationService, IStringLocalizer<LicenseSeatReportDataSet> stringLocalizer)
        : base(new DataSetDescriptor(DataSetName, stringLocalizer["Seat holders"], stringLocalizer["One row per seat given or held for an invited person."]), session, authorizationService, LearningCommerceConstants.CollectionName, LearningPermissions.ViewLearningReports)
    {
        var S = stringLocalizer;
        var group = S["Seat"].Value;

        AddField("SeatId", S["Seat ID"], DataFieldType.Text, record => record.ItemId, group).Identifier();
        AddField("PoolId", S["Pool"], DataFieldType.Text, record => record.PoolId, group).Identifier(new DataFieldReference(LearningCommerceConstants.DataSourceName, LicensePoolReportDataSet.DataSetName, "PoolId"));
        AddField("AccountId", S["Account"], DataFieldType.Text, record => record.AccountId, group).Identifier(ReportDataReferences.ContentItem(AccountsConstants.ContentTypes.Account));
        AddField("UserId", S["Person"], DataFieldType.Text, record => record.UserId, group).Identifier(ReportDataReferences.User());
        AddField("State", S["State"], DataFieldType.Text, record => record.State.ToString(), group);
        AddField("GivenBy", S["Given by"], DataFieldType.Text, record => record.GivenBy, group);
        AddField("GivenUtc", S["Given"], DataFieldType.DateTime, record => record.GivenUtc, group);
        AddField("RemovedUtc", S["Taken back"], DataFieldType.DateTime, record => record.RemovedUtc, group);

        CompleteDescription();
    }

    protected override IReadOnlyDictionary<string, Expression<Func<LicenseSeatIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<LicenseSeatIndex, string>>>(StringComparer.Ordinal)
        {
            ["SeatId"] = index => index.ItemId,
            ["PoolId"] = index => index.PoolId,
            ["AccountId"] = index => index.AccountId,
            ["UserId"] = index => index.UserId,
        };
}
