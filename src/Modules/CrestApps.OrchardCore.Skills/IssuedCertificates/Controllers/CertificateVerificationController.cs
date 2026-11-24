using CrestApps.OrchardCore.Skills.IssuedCertificates.Services;
using CrestApps.OrchardCore.Skills.IssuedCertificates.ViewModels;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Modules;
using OrchardCore.RateLimits;

namespace CrestApps.OrchardCore.Skills.IssuedCertificates.Controllers;

/// <summary>
/// The public page that proves an issued certificate from its code: who holds it, which certification, who issued it,
/// its dates and whether it is valid, expired or revoked, and nothing more.
/// </summary>
[Feature(SkillsConstants.Features.IssuedCertificates)]
public sealed class CertificateVerificationController : Controller
{
    private readonly ICertificateIssuer _issuer;
    private readonly ICertificationManager _certificationManager;
    private readonly CertificateDocumentBuilder _documentBuilder;
    private readonly ILocalClock _localClock;

    public CertificateVerificationController(
        ICertificateIssuer issuer,
        ICertificationManager certificationManager,
        CertificateDocumentBuilder documentBuilder,
        ILocalClock localClock)
    {
        _issuer = issuer;
        _certificationManager = certificationManager;
        _documentBuilder = documentBuilder;
        _localClock = localClock;
    }

    [HttpGet("certificates/verify")]
    [RateLimitGroup(SkillsConstants.VerificationRateLimitGroup)]
    public async Task<IActionResult> Index(string code)
    {
        Response.Headers["X-Robots-Tag"] = "noindex, nofollow";

        if (string.IsNullOrWhiteSpace(code))
        {
            return View(new CertificateVerificationViewModel());
        }

        var normalized = CertificateVerificationCodes.Normalize(code);

        if (normalized is not null && await _issuer.FindByVerificationCodeAsync(normalized, HttpContext.RequestAborted) is not null)
        {
            return RedirectToAction(nameof(Show), new { code = CertificateVerificationCodes.Format(normalized) });
        }

        return View(new CertificateVerificationViewModel { Code = code, NotFound = true });
    }

    [HttpGet("certificates/verify/{code}")]
    [RateLimitGroup(SkillsConstants.VerificationRateLimitGroup)]
    public async Task<IActionResult> Show(string code)
    {
        Response.Headers["X-Robots-Tag"] = "noindex, nofollow";

        var record = await _issuer.FindByVerificationCodeAsync(code, HttpContext.RequestAborted);

        if (record is null)
        {
            return View(nameof(Index), new CertificateVerificationViewModel { Code = code, NotFound = true });
        }

        var certification = await _certificationManager.FindByIdAsync(record.CertificationId);
        var today = DateOnly.FromDateTime((await _localClock.GetLocalNowAsync()).DateTime);

        return View(nameof(Index), new CertificateVerificationViewModel
        {
            Code = CertificateVerificationCodes.Format(record.VerificationCode),
            Found = true,
            HolderName = record.SubjectName,
            CertificationName = certification?.Name,
            Issuer = certification?.Issuer,
            Number = record.Number,
            IssuedOn = record.IssuedOn,
            ExpiresOn = record.ExpiresOn,
            State = CertificateVerificationViewModel.GetState(record, today),
            RevokedOn = record.RevokedUtc is { } revokedUtc ? DateOnly.FromDateTime((await _localClock.ConvertToLocalAsync(revokedUtc)).DateTime) : null,
            Renewed = !string.IsNullOrEmpty(record.RenewedByRecordId),
        });
    }

    [HttpGet("certificates/verify/{code}/certificate.pdf")]
    [RateLimitGroup(SkillsConstants.VerificationRateLimitGroup)]
    public async Task<IActionResult> Pdf(string code)
    {
        var record = await _issuer.FindByVerificationCodeAsync(code, HttpContext.RequestAborted);

        // A revoked certificate is not handed out again: the verification page says it was revoked.
        if (record is null || record.Status == CertificationStatus.Revoked)
        {
            return NotFound();
        }

        var certification = await _certificationManager.FindByIdAsync(record.CertificationId);

        if (certification is null)
        {
            return NotFound();
        }

        var url = Url.Action(nameof(Show), "CertificateVerification", new { area = "CrestApps.OrchardCore.Skills", code = CertificateVerificationCodes.Format(record.VerificationCode) }, Request.Scheme);
        var bytes = await _documentBuilder.BuildAsync(record, certification, url);

        Response.Headers["X-Robots-Tag"] = "noindex, nofollow";

        return File(bytes, "application/pdf");
    }
}
