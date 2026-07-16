using System.Linq.Expressions;
using CrestApps.OrchardCore.PhoneNumbers.Core;
using CrestApps.OrchardCore.PhoneNumbers.Core.Indexes;
using CrestApps.OrchardCore.PhoneNumbers.Core.Permissions;
using CrestApps.OrchardCore.PhoneNumbers.Core.Services;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;
using YesSql;

namespace CrestApps.OrchardCore.PhoneNumbers.Verifications.Reports;

/// <summary>
/// Contributes the phone number verification operational report to the shared admin Reports area.
/// </summary>
public sealed class PhoneNumberVerificationReportProvider : IReport
{
    private readonly ISession _session;
    private readonly PhoneNumberVerificationProviderOptions _providerOptions;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="PhoneNumberVerificationReportProvider"/> class.
    /// </summary>
    /// <param name="session">The YesSql session used to query the verification index.</param>
    /// <param name="providerOptions">The registered verification providers.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public PhoneNumberVerificationReportProvider(
        ISession session,
        IOptions<PhoneNumberVerificationProviderOptions> providerOptions,
        IClock clock,
        IStringLocalizer<PhoneNumberVerificationReportProvider> stringLocalizer)
    {
        _session = session;
        _providerOptions = providerOptions.Value;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public string Name => "phone-number-verifications";

    /// <inheritdoc/>
    public LocalizedString DisplayName => S["Phone Number Verifications"];

    /// <inheritdoc/>
    public LocalizedString Description => S["Current verification inventory, line-type mix, revalidation backlog, and enabled-provider usage for stored phone numbers."];

    /// <inheritdoc/>
    public string Category => string.Empty;

    /// <inheritdoc/>
    public Permission Permission => PhoneNumberVerificationsPermissions.RunPhoneNumberVerificationsReport;

    /// <inheritdoc/>
    public async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;

        var total = await CountAsync(_ => true);
        var verified = await CountAsync(index => index.IsVerified);
        var invalid = await CountAsync(index => index.VerificationStatus == PhoneNumberVerificationStatus.Invalid);
        var failures = await CountAsync(index => index.VerificationStatus == PhoneNumberVerificationStatus.Failed);
        var pending = await CountAsync(index => index.VerificationStatus == PhoneNumberVerificationStatus.Unverified);
        var requiringRevalidation = await CountAsync(index =>
            index.LastVerifiedUtc != null
            && index.NextVerificationDueUtc != null
            && index.NextVerificationDueUtc <= now);
        var mobile = await CountAsync(index => index.IsMobile);
        var landline = await CountAsync(index => index.IsLandline);
        var voip = await CountAsync(index => index.IsVoip);
        var attempts = verified + invalid + failures;

        var document = new ReportDocument();

        var summary = ReportSection.ForMetrics(S["Verification status"].Value,
        [
            new ReportMetric(S["Total contacts"].Value, ReportFormat.Number(total)),
            new ReportMetric(S["Verified numbers"].Value, ReportFormat.Number(verified)),
            new ReportMetric(S["Unverified numbers"].Value, ReportFormat.Number(total - verified)),
            new ReportMetric(S["Invalid numbers"].Value, ReportFormat.Number(invalid)),
            new ReportMetric(S["Verification failures"].Value, ReportFormat.Number(failures)),
            new ReportMetric(
                S["Success rate"].Value,
                ReportFormat.Percent(ComputeSuccessRate(verified, invalid, failures)),
                attempts == 0 ? null : S["{0} completed attempts", ReportFormat.Number(attempts)].Value),
        ]);
        summary.Description = S["This report reflects the current verification state across all indexed phone-number records."].Value;
        document.Add(summary);

        document.Add(ReportSection.ForMetrics(S["Line and queue status"].Value,
        [
            new ReportMetric(S["Mobile numbers"].Value, ReportFormat.Number(mobile)),
            new ReportMetric(S["Landline numbers"].Value, ReportFormat.Number(landline)),
            new ReportMetric(S["VoIP numbers"].Value, ReportFormat.Number(voip)),
            new ReportMetric(S["Pending verification"].Value, ReportFormat.Number(pending)),
            new ReportMetric(S["Requiring revalidation"].Value, ReportFormat.Number(requiringRevalidation)),
        ]));

        if (_providerOptions.Providers.Count > 0)
        {
            var rows = new List<ReportRow>();

            foreach (var provider in _providerOptions.Providers.Values)
            {
                var usage = await CountAsync(index => index.VerificationProvider == provider.Key);
                var displayName = provider.DisplayName?.Value;

                rows.Add(new ReportRow(
                [
                    string.IsNullOrEmpty(displayName) ? provider.Key : displayName,
                    ReportFormat.Number(usage),
                ]));
            }

            document.Add(ReportSection.ForTable(
                S["Provider usage"].Value,
                [
                    new ReportColumn(S["Provider"].Value),
                    new ReportColumn(S["Records"].Value, ReportColumnAlign.End),
                ],
                rows));
        }

        return document;
    }

    private Task<int> CountAsync(Expression<Func<PhoneNumberVerificationPartIndex, bool>> predicate)
        => _session.QueryIndex(predicate).CountAsync();

    private static double ComputeSuccessRate(int verified, int invalid, int failures)
    {
        var attempts = verified + invalid + failures;

        return attempts == 0
            ? 0
            : verified / (double)attempts;
    }
}
