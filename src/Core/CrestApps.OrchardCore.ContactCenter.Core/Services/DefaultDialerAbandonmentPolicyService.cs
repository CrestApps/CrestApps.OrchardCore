using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default <see cref="IDialerAbandonmentPolicyService"/> implementation. It enforces the
/// rolling abandonment-rate cap only for automated pacing modes, honors a sample floor to avoid volatile
/// suppression, and fails closed when a cap is enforced but the statistics cannot be proven.
/// </summary>
public sealed class DefaultDialerAbandonmentPolicyService : IDialerAbandonmentPolicyService
{
    private readonly IEnumerable<IDialerAbandonmentStatisticsProvider> _statisticsProviders;
    private readonly ContactCenterComplianceOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultDialerAbandonmentPolicyService"/> class.
    /// </summary>
    /// <param name="statisticsProviders">The registered rolling abandonment statistics providers, if any.</param>
    /// <param name="options">The tenant-level compliance options.</param>
    public DefaultDialerAbandonmentPolicyService(
        IEnumerable<IDialerAbandonmentStatisticsProvider> statisticsProviders,
        IOptions<ContactCenterComplianceOptions> options)
    {
        _statisticsProviders = statisticsProviders;
        _options = options.Value;
    }

    /// <inheritdoc/>
    public async Task<DialerAbandonmentEvaluation> EvaluateAsync(DialerProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (!profile.EnforceAbandonmentCap)
        {
            return DialerAbandonmentEvaluation.Permitted(true, 0, 0, "The abandonment cap is not enforced for this profile.");
        }

        if (!profile.Mode.IsAutomated())
        {
            return DialerAbandonmentEvaluation.Permitted(
                true,
                0,
                0,
                "Manual and preview dialing bind an agent to every call and cannot abandon a connected party.");
        }

        var window = TimeSpan.FromMinutes(_options.AbandonmentRollingWindowMinutes);
        var statistics = await ResolveStatisticsAsync(profile.ItemId, window, cancellationToken);

        if (statistics is null)
        {
            return DialerAbandonmentEvaluation.Suppressed(
                false,
                0,
                0,
                "The rolling abandonment statistics required to prove compliance were unavailable.");
        }

        if (statistics.LiveAnswers < profile.AbandonmentSampleFloor)
        {
            return DialerAbandonmentEvaluation.Permitted(
                true,
                0,
                statistics.LiveAnswers,
                $"The rolling sample of {statistics.LiveAnswers} live answers is below the {profile.AbandonmentSampleFloor}-call floor.");
        }

        var ratePercent = statistics.LiveAnswers == 0
            ? 0
            : (double)statistics.AbandonedCalls / statistics.LiveAnswers * 100;

        if (ratePercent > profile.MaxAbandonmentRatePercent)
        {
            return DialerAbandonmentEvaluation.Suppressed(
                true,
                ratePercent,
                statistics.LiveAnswers,
                $"The rolling abandonment rate of {ratePercent:0.##}% exceeds the {profile.MaxAbandonmentRatePercent:0.##}% cap.");
        }

        return DialerAbandonmentEvaluation.Permitted(
            true,
            ratePercent,
            statistics.LiveAnswers,
            $"The rolling abandonment rate of {ratePercent:0.##}% is within the {profile.MaxAbandonmentRatePercent:0.##}% cap.");
    }

    private async Task<DialerAbandonmentStatistics> ResolveStatisticsAsync(
        string dialerProfileId,
        TimeSpan window,
        CancellationToken cancellationToken)
    {
        foreach (var provider in _statisticsProviders)
        {
            var statistics = await provider.GetStatisticsAsync(dialerProfileId, window, cancellationToken);

            if (statistics is not null)
            {
                return statistics;
            }
        }

        return null;
    }
}
