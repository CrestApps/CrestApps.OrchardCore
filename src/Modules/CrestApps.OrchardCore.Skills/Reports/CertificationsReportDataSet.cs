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
/// One row per certification: its name, issuer, validity and reminders.
/// </summary>
public sealed class CertificationsReportDataSet : IndexedReportDataSet<Certification, CertificationIndex>, ISkillsReportDataSet
{
    /// <summary>
    /// The name of the data set.
    /// </summary>
    public const string DataSetName = "SkillsCertifications";

    /// <summary>
    /// Initializes a new instance of the <see cref="CertificationsReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CertificationsReportDataSet(ISession session, IAuthorizationService authorizationService, IStringLocalizer<CertificationsReportDataSet> stringLocalizer)
        : base(new DataSetDescriptor(DataSetName, stringLocalizer["Certifications"], stringLocalizer["One row per certification: its name, who issues it, how long a certificate lasts and when reminders are raised."]), session, authorizationService, SkillsConstants.CollectionName, SkillsPermissions.ViewSkills)
    {
        var S = stringLocalizer;
        var group = S["Certification"].Value;

        AddField("CertificationId", S["Certification ID"], DataFieldType.Text, record => record.ItemId, group).Identifier();
        AddField("Name", S["Name"], DataFieldType.Text, record => record.Name, group);
        AddField("Issuer", S["Issued by"], DataFieldType.Text, record => record.Issuer, group);
        AddField("ValidityMonths", S["Valid for (months)"], DataFieldType.Integer, record => record.ValidityMonths, group);
        AddField("ReminderDays", S["Reminder days"], DataFieldType.Text, record => string.Join(", ", record.ReminderDays), group);
        AddField("GenerateNumbers", S["Numbers issued certificates"], DataFieldType.Boolean, record => record.GenerateNumbers, group);
        AddField("Enabled", S["Enabled"], DataFieldType.Boolean, record => record.Enabled, group);

        CompleteDescription();
    }

    /// <inheritdoc />
    protected override IReadOnlyDictionary<string, Expression<Func<CertificationIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<CertificationIndex, string>>>(StringComparer.Ordinal)
        {
            ["CertificationId"] = index => index.ItemId,
        };
}
