using System.Linq.Expressions;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using CrestApps.OrchardCore.Skills.Core.Indexes;
using CrestApps.OrchardCore.Skills.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Skills.Reports;

/// <summary>
/// One row per certificate a person holds, recorded by hand or issued by the site, with its dates, status, source and
/// renewal links. The verification code is left out: it is the holder's proof.
/// </summary>
public sealed class CertificatesReportDataSet : IndexedReportDataSet<CertificationRecord, CertificationRecordIndex>, ISkillsReportDataSet
{
    /// <summary>
    /// The name of the data set.
    /// </summary>
    public const string DataSetName = "SkillsCertificates";

    /// <summary>
    /// Initializes a new instance of the <see cref="CertificatesReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CertificatesReportDataSet(ISession session, IAuthorizationService authorizationService, IStringLocalizer<CertificatesReportDataSet> stringLocalizer)
        : base(new DataSetDescriptor(DataSetName, stringLocalizer["Certificates"], stringLocalizer["One row per certificate a person holds, with the certification, its number, dates and status, what earned it and the certificates it renews or is renewed by."]), session, authorizationService, SkillsConstants.CollectionName, SkillsPermissions.ViewSkills)
    {
        var S = stringLocalizer;
        var group = S["Certificate"].Value;

        AddField("CertificateId", S["Certificate ID"], DataFieldType.Text, record => record.ItemId, group).Identifier();
        AddField("CertificationId", S["Certification"], DataFieldType.Text, record => record.CertificationId, group).Identifier();
        AddField("SubjectKind", S["Holder kind"], DataFieldType.Text, record => record.SubjectKind, group);
        AddField("SubjectId", S["Holder"], DataFieldType.Text, record => record.SubjectId, group).Identifier();
        AddField("SubjectName", S["Holder name"], DataFieldType.Text, record => record.SubjectName, group);
        AddField("Number", S["Number"], DataFieldType.Text, record => record.Number, group);
        AddField("IssuedOn", S["Issued on"], DataFieldType.Date, record => ToDateTime(record.IssuedOn), group);
        AddField("ExpiresOn", S["Expires on"], DataFieldType.Date, record => ToDateTime(record.ExpiresOn), group);
        AddField("Status", S["Status"], DataFieldType.Text, record => record.Status.ToString(), group);
        AddField("Source", S["Source"], DataFieldType.Text, record => record.Source, group);
        AddField("SourceId", S["Source ID"], DataFieldType.Text, record => record.SourceId, group);
        AddField("Evidence", S["Evidence"], DataFieldType.Text, record => record.Evidence, group);
        AddField("PreviousCertificateId", S["Renews"], DataFieldType.Text, record => record.PreviousRecordId, group).Identifier();
        AddField("RenewedByCertificateId", S["Renewed by"], DataFieldType.Text, record => record.RenewedByRecordId, group).Identifier();
        AddField("RevokedReason", S["Revocation reason"], DataFieldType.Text, record => record.RevokedReason, group);
        AddField("RevokedUtc", S["Revoked"], DataFieldType.DateTime, record => record.RevokedUtc, group);
        AddField("CreatedUtc", S["Recorded"], DataFieldType.DateTime, record => record.CreatedUtc, group);

        CompleteDescription();
    }

    /// <inheritdoc />
    protected override IReadOnlyDictionary<string, Expression<Func<CertificationRecordIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<CertificationRecordIndex, string>>>(StringComparer.Ordinal)
        {
            ["CertificateId"] = index => index.ItemId,
            ["CertificationId"] = index => index.CertificationId,
            ["SubjectId"] = index => index.SubjectId,
        };

    private static DateTime? ToDateTime(DateOnly? date)
        => date?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
}
