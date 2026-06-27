using System.Linq.Expressions;
using CrestApps.OrchardCore.PhoneNumbers.Core.Indexes;
using CrestApps.OrchardCore.PhoneNumbers.Core.Permissions;
using CrestApps.OrchardCore.PhoneNumbers.Core.Services;
using CrestApps.OrchardCore.PhoneNumbers.Verifications.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.PhoneNumbers.Verifications.Controllers;

/// <summary>
/// Admin controller that renders operational metrics for phone number verifications.
/// </summary>
[Admin]
public sealed class ReportsController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly ISession _session;
    private readonly PhoneNumberVerificationProviderOptions _providerOptions;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportsController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="session">The YesSql session used to query the verification index.</param>
    /// <param name="providerOptions">The registered verification providers.</param>
    /// <param name="clock">The clock.</param>
    public ReportsController(
        IAuthorizationService authorizationService,
        ISession session,
        IOptions<PhoneNumberVerificationProviderOptions> providerOptions,
        IClock clock)
    {
        _authorizationService = authorizationService;
        _session = session;
        _providerOptions = providerOptions.Value;
        _clock = clock;
    }

    /// <summary>
    /// Renders the phone number verification report dashboard.
    /// </summary>
    /// <returns>The report view.</returns>
    public async Task<IActionResult> Index()
    {
        if (!await _authorizationService.AuthorizeAsync(User, PhoneNumberVerificationsPermissions.RunPhoneNumberVerificationsReport))
        {
            return Forbid();
        }

        var now = _clock.UtcNow;

        var total = await CountAsync(_ => true);
        var verified = await CountAsync(index => index.IsVerified);
        var invalid = await CountAsync(index => index.VerificationStatus == PhoneNumberVerificationStatus.Invalid);
        var failures = await CountAsync(index => index.VerificationStatus == PhoneNumberVerificationStatus.Failed);
        var pending = await CountAsync(index => index.VerificationStatus == PhoneNumberVerificationStatus.Unverified);
        var requiring = await CountAsync(index =>
            index.LastVerifiedUtc != null
            && index.NextVerificationDueUtc != null
            && index.NextVerificationDueUtc <= now);
        var mobile = await CountAsync(index => index.IsMobile);
        var landline = await CountAsync(index => index.IsLandline);
        var voip = await CountAsync(index => index.IsVoip);

        var model = new PhoneNumberVerificationReportViewModel
        {
            TotalContacts = total,
            VerifiedNumbers = verified,
            UnverifiedNumbers = total - verified,
            InvalidNumbers = invalid,
            MobileNumbers = mobile,
            LandlineNumbers = landline,
            VoipNumbers = voip,
            PendingVerification = pending,
            RequiringRevalidation = requiring,
            VerificationFailures = failures,
            VerificationSuccessRate = ComputeSuccessRate(verified, invalid, failures),
        };

        foreach (var provider in _providerOptions.Providers.Values)
        {
            var usage = await CountAsync(index => index.VerificationProvider == provider.Key);

            var displayName = provider.DisplayName?.Value;

            model.ProviderUsageCounts[string.IsNullOrEmpty(displayName) ? provider.Key : displayName] = usage;
        }

        return View(model);
    }

    private Task<int> CountAsync(Expression<Func<PhoneNumberVerificationPartIndex, bool>> predicate)
        => _session.QueryIndex(predicate).CountAsync();

    private static double ComputeSuccessRate(int verified, int invalid, int failures)
    {
        var attempts = verified + invalid + failures;

        return attempts == 0
            ? 0
            : Math.Round(verified * 100d / attempts, 2);
    }
}
