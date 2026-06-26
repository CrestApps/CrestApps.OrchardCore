using CrestApps.OrchardCore.PhoneNumberVerifications.Indexes;
using CrestApps.OrchardCore.PhoneNumberVerifications.Services;
using CrestApps.OrchardCore.PhoneNumberVerifications.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Admin;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.PhoneNumberVerifications.Controllers;

/// <summary>
/// Admin controller that renders operational metrics for phone number verifications.
/// </summary>
[Admin]
public sealed class ReportController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly ISession _session;
    private readonly IPhoneNumberVerificationManager _verificationManager;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="session">The YesSql session used to query the verification index.</param>
    /// <param name="verificationManager">The verification manager used to enumerate providers.</param>
    /// <param name="clock">The clock.</param>
    public ReportController(
        IAuthorizationService authorizationService,
        ISession session,
        IPhoneNumberVerificationManager verificationManager,
        IClock clock)
    {
        _authorizationService = authorizationService;
        _session = session;
        _verificationManager = verificationManager;
        _clock = clock;
    }

    /// <summary>
    /// Renders the phone number verification report dashboard.
    /// </summary>
    /// <returns>The report view.</returns>
    public async Task<IActionResult> Index()
    {
        if (!await _authorizationService.AuthorizeAsync(User, PhoneNumberVerificationsPermissions.VerifyPhoneNumbers))
        {
            return Forbid();
        }

        var now = _clock.UtcNow;

        var total = await CountAsync(_ => true);
        var verified = await CountAsync(index => index.IsVerified);
        var invalid = await CountAsync(index => index.VerificationStatus == PhoneNumberVerificationStatus.Invalid);
        var failures = await CountAsync(index => index.VerificationStatus == PhoneNumberVerificationStatus.Failed);
        var pending = await CountAsync(index => index.LastVerifiedUtc == null);
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

        foreach (var provider in _verificationManager.GetProviders())
        {
            var usage = await CountAsync(index => index.VerificationProvider == provider.Key);

            model.ProviderUsageCounts[provider.DisplayName ?? provider.Key] = usage;
        }

        return View(model);
    }

    private Task<int> CountAsync(System.Linq.Expressions.Expression<Func<PhoneNumberVerificationPartIndex, bool>> predicate)
        => _session.QueryIndex(predicate).CountAsync();

    private static double ComputeSuccessRate(int verified, int invalid, int failures)
    {
        var attempts = verified + invalid + failures;

        return attempts == 0
            ? 0
            : Math.Round(verified * 100d / attempts, 2);
    }
}
