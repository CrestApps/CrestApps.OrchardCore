using System.Linq.Expressions;
using CrestApps.OrchardCore.Learning.Credentials.Core.Indexes;
using CrestApps.OrchardCore.Learning.Credentials.Models;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using OrchardCore.Localization;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Learning.Credentials.Reports;

/// <summary>
/// The Orchard Core data source of Digital Credentials, for Data Pipelines and the Report Builder.
/// </summary>
public sealed class LearningCredentialsDataSource : RecordDataSource, IDataSource
{
    /// <summary>
    /// The name of the data source.
    /// </summary>
    public const string SourceName = "Learning.Credentials";

    private readonly IRecordDataSet[] _dataSets;

    /// <summary>
    /// Initializes a new instance of the <see cref="LearningCredentialsDataSource"/> class.
    /// </summary>
    /// <param name="awards">The credential awards data set.</param>
    public LearningCredentialsDataSource(CredentialAwardsReportDataSet awards)
    {
        _dataSets = [awards];
    }

    /// <summary>
    /// Gets the name of the data source.
    /// </summary>
    public static string Name => SourceName;

    /// <summary>
    /// Gets the display name.
    /// </summary>
    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<LearningCredentialsDataSource>("Digital credentials");

    /// <summary>
    /// Gets the description.
    /// </summary>
    public static LocalizationSource Description { get; } = LocalizationSource.Create<LearningCredentialsDataSource>("The digital credentials the site awarded: which credential, to whom, how, when, until when, and whether it was revoked.");

    /// <inheritdoc/>
    protected override IEnumerable<IRecordDataSet> DataSets => _dataSets;
}

/// <summary>
/// The Credential awards data set: one row per award. It holds no e-mail address and no identity hash.
/// </summary>
public sealed class CredentialAwardsReportDataSet : IndexedReportDataSet<CredentialAward, CredentialAwardIndex, CredentialAward>
{
    /// <summary>
    /// The name of the data set, which is never renamed.
    /// </summary>
    public const string DataSetName = "CredentialAwards";

    /// <summary>
    /// Initializes a new instance of the <see cref="CredentialAwardsReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The localizer.</param>
    public CredentialAwardsReportDataSet(ISession session, IAuthorizationService authorizationService, IStringLocalizer<CredentialAwardsReportDataSet> stringLocalizer)
        : base(new DataSetDescriptor(DataSetName, stringLocalizer["Credential awards"], stringLocalizer["One row per digital credential award, with the credential, the recipient's user id, how it was awarded and its dates."]), session, authorizationService, CredentialsConstants.CollectionName, LearningCredentialsPermissions.ViewCredentialAwards)
    {
        var S = stringLocalizer;
        var group = S["Award"].Value;

        AddField("AwardId", S["Award ID"], DataFieldType.Text, award => award.ItemId, group).Identifier();
        AddField("DefinitionId", S["Credential definition ID"], DataFieldType.Text, award => award.DefinitionId, group).Identifier();
        AddField("Credential", S["Credential"], DataFieldType.Text, award => award.Achievement?.Name, group);
        AddField("AchievementType", S["Achievement type"], DataFieldType.Text, award => award.Achievement?.AchievementType, group);
        AddField("UserId", S["Recipient"], DataFieldType.Text, award => award.RecipientUserId, group).Identifier(ReportDataReferences.User());
        AddField("RuleKind", S["Awarded by rule"], DataFieldType.Text, award => award.RuleKind, group);
        AddField("SourceId", S["Source ID"], DataFieldType.Text, award => award.SourceId, group);
        AddField("AwardedBy", S["Awarded by"], DataFieldType.Text, award => award.AwardedBy, group).Identifier(ReportDataReferences.User());
        AddField("IssuedUtc", S["Issued"], DataFieldType.DateTime, award => award.IssuedUtc, group);
        AddField("ExpiresUtc", S["Expires"], DataFieldType.DateTime, award => award.ExpiresUtc, group);
        AddField("Revoked", S["Revoked"], DataFieldType.Boolean, award => award.IsRevoked, group);
        AddField("RevokedUtc", S["Revoked on"], DataFieldType.DateTime, award => award.RevokedUtc, group);
        AddField("RevokedBy", S["Revoked by"], DataFieldType.Text, award => award.RevokedBy, group).Identifier(ReportDataReferences.User());
        AddField("PersonalDataErased", S["Personal data erased"], DataFieldType.Boolean, award => award.ErasedUtc is not null, group);

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override (string Field, Expression<Func<CredentialAwardIndex, DateTime>> Column)? DateColumn
        => ("IssuedUtc", index => index.IssuedUtc);

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<CredentialAwardIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<CredentialAwardIndex, string>>>(StringComparer.Ordinal)
        {
            ["AwardId"] = index => index.ItemId,
            ["DefinitionId"] = index => index.DefinitionId,
            ["UserId"] = index => index.RecipientUserId,
        };

    /// <inheritdoc/>
    protected override IEnumerable<CredentialAward> GetRows(CredentialAward document)
    {
        yield return document;
    }
}
