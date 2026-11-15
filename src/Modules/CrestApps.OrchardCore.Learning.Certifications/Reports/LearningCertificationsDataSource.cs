using System.Linq.Expressions;
using CrestApps.OrchardCore.Learning.Certifications.Core.Indexes;
using CrestApps.OrchardCore.Learning.Certifications.Models;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using OrchardCore.Localization;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Learning.Certifications.Reports;

/// <summary>
/// The Learning certifications data source: one row per person on a certification path.
/// </summary>
public sealed class LearningCertificationsDataSource : RecordDataSource, IDataSource
{
    private readonly IRecordDataSet[] _dataSets;

    /// <summary>
    /// Initializes a new instance of the <see cref="LearningCertificationsDataSource"/> class.
    /// </summary>
    /// <param name="progress">The progress data set.</param>
    public LearningCertificationsDataSource(CertificationProgressReportDataSet progress)
    {
        _dataSets = [progress];
    }

    /// <summary>
    /// Gets the name of the data source.
    /// </summary>
    public static string Name => CertificationsConstants.DataSourceName;

    /// <summary>
    /// Gets the display name.
    /// </summary>
    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<LearningCertificationsDataSource>("Learning certifications");

    /// <summary>
    /// Gets the description.
    /// </summary>
    public static LocalizationSource Description { get; } = LocalizationSource.Create<LearningCertificationsDataSource>("Where each person stands on each certification path: state, cycle, current certificate and its dates.");

    /// <inheritdoc />
    protected override IEnumerable<IRecordDataSet> DataSets => _dataSets;
}

/// <summary>
/// One row per person on a certification path.
/// </summary>
public sealed class CertificationProgressReportDataSet : IndexedReportDataSet<CertificationPathProgress, CertificationPathProgressIndex>
{
    /// <summary>
    /// The name of the data set.
    /// </summary>
    public const string DataSetName = "LearningCertificationProgress";

    /// <summary>
    /// Initializes a new instance of the <see cref="CertificationProgressReportDataSet"/> class.
    /// </summary>
    public CertificationProgressReportDataSet(ISession session, IAuthorizationService authorizationService, IStringLocalizer<CertificationProgressReportDataSet> stringLocalizer)
        : base(new DataSetDescriptor(DataSetName, stringLocalizer["Certification progress"], stringLocalizer["One row per person on a certification path: where they stand, the cycle, the current certificate and when it expires and renewal opens."]), session, authorizationService, CertificationsConstants.CollectionName, LearningPermissions.ViewLearningReports)
    {
        var S = stringLocalizer;
        var group = S["Progress"].Value;

        AddField("ProgressId", S["Progress ID"], DataFieldType.Text, record => record.ItemId, group).Identifier();
        AddField("PathId", S["Path"], DataFieldType.Text, record => record.PathId, group).Identifier();
        AddField("CertificationId", S["Certification"], DataFieldType.Text, record => record.CertificationId, group).Identifier();
        AddField("UserId", S["Person"], DataFieldType.Text, record => record.UserId, group).Identifier(ReportDataReferences.User());
        AddField("State", S["State"], DataFieldType.Text, record => record.State.ToString(), group);
        AddField("Cycle", S["Cycle"], DataFieldType.Integer, record => record.Cycle, group);
        AddField("CurrentCertificateId", S["Current certificate"], DataFieldType.Text, record => record.CurrentRecordId, group).Identifier();
        AddField("ExpiresOn", S["Expires on"], DataFieldType.Date, record => record.ExpiresOn?.ToDateTime(TimeOnly.MinValue), group);
        AddField("RenewalOpensOn", S["Renewal opens on"], DataFieldType.Date, record => record.RenewalOpensOn?.ToDateTime(TimeOnly.MinValue), group);
        AddField("StartedBy", S["Started by"], DataFieldType.Text, record => record.StartedBy, group);
        AddField("CreatedUtc", S["Started"], DataFieldType.DateTime, record => record.CreatedUtc, group);

        CompleteDescription();
    }

    /// <inheritdoc />
    protected override IReadOnlyDictionary<string, Expression<Func<CertificationPathProgressIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<CertificationPathProgressIndex, string>>>(StringComparer.Ordinal)
        {
            ["ProgressId"] = index => index.ItemId,
            ["PathId"] = index => index.PathId,
            ["CertificationId"] = index => index.CertificationId,
            ["UserId"] = index => index.UserId,
        };
}
