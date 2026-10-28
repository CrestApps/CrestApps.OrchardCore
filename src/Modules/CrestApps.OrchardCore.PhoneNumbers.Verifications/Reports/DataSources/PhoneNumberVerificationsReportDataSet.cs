using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using CrestApps.OrchardCore.PhoneNumbers.Core.Indexes;
using CrestApps.OrchardCore.PhoneNumbers.Core.Models;
using CrestApps.OrchardCore.PhoneNumbers.Core.Permissions;
using CrestApps.OrchardCore.PhoneNumbers.Core.Services;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.DataSources;
using OrchardCore.Settings;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.PhoneNumbers.Verifications.Reports.DataSources;

/// <summary>
/// The <c>Verifications</c> data set: one row per content item that carries phone number verification data, in its
/// latest version, with the phone number, its verification status, provider, dates and attempts, and the line details
/// the provider reported. These are the details the phone verifications queue shows, and only principals allowed to
/// run the phone number verifications report read them. The raw provider response is never exposed.
/// </summary>
/// <remarks>
/// The fields the index holds read exactly what the index provider writes, so the database filters and groups them
/// exactly as the rows do. No date of a verification is always set, so the data set has no main date and is read
/// newest first by its index rows.
/// </remarks>
public sealed class PhoneNumberVerificationsReportDataSet : IndexedReportDataSet<ContentItem, PhoneNumberVerificationPartIndex, PhoneNumberVerificationReportRow>
{
    private readonly ISiteService _siteService;

    /// <summary>
    /// Initializes a new instance of the <see cref="PhoneNumberVerificationsReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session the content items are read from.</param>
    /// <param name="siteService">The site service, which holds how many failed attempts in a row the site allows.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public PhoneNumberVerificationsReportDataSet(
        ISession session,
        ISiteService siteService,
        IAuthorizationService authorizationService,
        IStringLocalizer<PhoneNumberVerificationsReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                PhoneNumberVerificationReportDataSets.Verifications,
                stringLocalizer["Phone number verifications"],
                stringLocalizer["One row per record that carries a phone number to verify: the number, its verification status, provider, dates and attempts, and the line details the provider reported."]),
            session,
            authorizationService,
            null,
            PhoneNumberVerificationsPermissions.RunPhoneNumberVerificationsReport)
    {
        _siteService = siteService;

        var S = stringLocalizer;
        var record = S["Record"].Value;
        var phone = S["Phone number"].Value;
        var verification = S["Verification"].Value;
        var line = S["Line"].Value;

        AddField(PhoneNumberVerificationReportDataSets.ContentItemIdField, S["Content item ID"], DataFieldType.Text, row => row.ContentItem.ContentItemId, record).Identifier();
        AddField(nameof(ContentItem.DisplayText), S["Title"], DataFieldType.Text, row => row.ContentItem.DisplayText, record);
        AddField(nameof(ContentItem.ContentType), S["Content type"], DataFieldType.Text, row => row.ContentItem.ContentType, record);

        AddField(nameof(PhoneNumberVerificationPartIndex.PhoneNumber), S["Phone number"], DataFieldType.Text, row => GetPhoneNumber(row), phone);
        AddField(PhoneNumberVerificationReportDataSets.NormalizedPhoneNumberField, S["Normalized phone number"], DataFieldType.Text, row => GetNormalizedPhoneNumber(row), phone)
            .Description = S["The phone number in E.164 format, such as +14255551212."].Value;
        AddField(nameof(PhoneNumberVerificationPartIndex.CountryCode), S["Country"], DataFieldType.Text, row => row.Result?.CountryCode, phone);

        AddField(nameof(PhoneNumberVerificationPartIndex.VerificationStatus), S["Status"], DataFieldType.Text, row => row.Part.VerificationStatus, verification)
            .Description = S["Unverified, Verified, Invalid or Failed."].Value;
        AddField(nameof(PhoneNumberVerificationPartIndex.IsVerified), S["Verified"], DataFieldType.Boolean, row => row.Part.VerificationStatus == PhoneNumberVerificationStatus.Verified, verification);
        AddField(nameof(PhoneNumberVerificationPartIndex.VerificationProvider), S["Provider"], DataFieldType.Text, row => row.Part.VerificationProvider, verification);
        AddField(nameof(PhoneNumberVerificationPartIndex.LastVerifiedUtc), S["Last verified"], DataFieldType.DateTime, row => row.Part.LastVerifiedUtc, verification);
        AddField(nameof(PhoneNumberVerificationPartIndex.LastAttemptUtc), S["Last attempt"], DataFieldType.DateTime, row => row.Part.LastAttemptUtc, verification);
        AddField(nameof(PhoneNumberVerificationPartIndex.NextVerificationDueUtc), S["Next verification due"], DataFieldType.DateTime, row => row.Part.NextVerificationDueUtc, verification);
        AddField(nameof(PhoneNumberVerificationPart.VerificationAttemptCount), S["Attempts"], DataFieldType.Integer, row => row.Part.VerificationAttemptCount, verification);
        AddField(nameof(PhoneNumberVerificationPartIndex.FailedAttemptCount), S["Failed attempts in a row"], DataFieldType.Integer, row => row.Part.FailedAttemptCount, verification);
        AddField(nameof(PhoneNumberVerificationReportRow.IsExhausted), S["Retries exhausted"], DataFieldType.Boolean, row => row.IsExhausted, verification)
            .Description = S["The record failed as many times in a row as the site allows, so it is no longer retried automatically."].Value;
        AddField(nameof(PhoneNumberVerificationPart.LastError), S["Last error"], DataFieldType.Text, row => row.Part.LastError, verification);

        AddField(nameof(PhoneNumberVerificationResult.LineType), S["Line type"], DataFieldType.Text, row => row.Result?.LineType, line);
        AddField(nameof(PhoneNumberVerificationPartIndex.IsMobile), S["Mobile"], DataFieldType.Boolean, row => row.Result is not null && row.Result.IsMobile, line);
        AddField(nameof(PhoneNumberVerificationPartIndex.IsLandline), S["Landline"], DataFieldType.Boolean, row => row.Result is not null && row.Result.IsLandline, line);
        AddField(nameof(PhoneNumberVerificationPartIndex.IsVoip), S["VoIP"], DataFieldType.Boolean, row => row.Result is not null && row.Result.IsVoip, line);
        AddField(nameof(PhoneNumberVerificationPartIndex.Carrier), S["Carrier"], DataFieldType.Text, row => row.Result?.Carrier, line);
        AddField(nameof(PhoneNumberVerificationPartIndex.LineStatus), S["Line status"], DataFieldType.Text, row => row.Result?.LineStatus, line);
        AddField(nameof(PhoneNumberVerificationResult.MinimumAge), S["Minimum line age"], DataFieldType.Text, row => row.Result?.MinimumAge, line);

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<PhoneNumberVerificationPartIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<PhoneNumberVerificationPartIndex, string>>>(StringComparer.Ordinal)
        {
            [PhoneNumberVerificationReportDataSets.ContentItemIdField] = index => index.ContentItemId,
            [PhoneNumberVerificationReportDataSets.NormalizedPhoneNumberField] = index => index.NormalizedPhoneNumber,
        };

    /// <summary>
    /// Gets a value indicating whether the data set can group and aggregate itself in the database. It can: each
    /// content item gives one row, and every principal that may read the data set reads every row.
    /// </summary>
    protected override bool CanAggregate => true;

    /// <inheritdoc/>
    protected override void DescribeColumns(ReportIndexConditions<PhoneNumberVerificationPartIndex> columns)
    {
        columns
            .Enum(nameof(PhoneNumberVerificationPartIndex.VerificationStatus), index => index.VerificationStatus)
            .Text(nameof(PhoneNumberVerificationPartIndex.VerificationProvider), nameof(PhoneNumberVerificationPartIndex.VerificationProvider))
            .Text(nameof(PhoneNumberVerificationPartIndex.CountryCode), nameof(PhoneNumberVerificationPartIndex.CountryCode))
            .Text(nameof(PhoneNumberVerificationPartIndex.LineStatus), nameof(PhoneNumberVerificationPartIndex.LineStatus));
    }

    /// <inheritdoc/>
    protected override IEnumerable<PhoneNumberVerificationReportRow> GetRows(ContentItem document)
    {
        if (!document.TryGet<PhoneNumberVerificationPart>(out var part))
        {
            yield break;
        }

        part.TryGetPhoneNumberVerificationResult(out var result);

        yield return new PhoneNumberVerificationReportRow(document, part, result);
    }

    /// <inheritdoc/>
    protected override async IAsyncEnumerable<PhoneNumberVerificationReportRow> LoadAsync(DataSourceQuery query, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var maxAttempts = PhoneNumberVerificationsSettings.DefaultMaxVerificationAttempts;

        if (query.Fields is null || query.Fields.Count == 0 || query.Fields.Contains(nameof(PhoneNumberVerificationReportRow.IsExhausted)))
        {
            maxAttempts = (await _siteService.GetSettingsAsync<PhoneNumberVerificationsSettings>()).MaxVerificationAttempts;
        }

        await foreach (var row in base.LoadAsync(query, cancellationToken))
        {
            row.IsExhausted = row.Part.HasReachedMaxVerificationAttempts(maxAttempts);

            yield return row;
        }
    }

    // The index keeps the result's number when the part has none.
    private static string GetPhoneNumber(PhoneNumberVerificationReportRow row)
    {
        return row.Part.PhoneNumber ?? row.Result?.PhoneNumber;
    }

    // The index keeps the result's normalized number, or else its number, when the part has none.
    private static string GetNormalizedPhoneNumber(PhoneNumberVerificationReportRow row)
    {
        if (row.Part.NormalizedPhoneNumber is not null || row.Result is null)
        {
            return row.Part.NormalizedPhoneNumber;
        }

        return row.Result.NormalizedPhoneNumber ?? row.Result.PhoneNumber;
    }
}
