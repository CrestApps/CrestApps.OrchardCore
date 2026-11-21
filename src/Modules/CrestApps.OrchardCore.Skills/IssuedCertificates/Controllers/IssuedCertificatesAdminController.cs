using CrestApps.OrchardCore.Numbering;
using CrestApps.OrchardCore.Skills.IssuedCertificates.Services;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Admin;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Skills.IssuedCertificates.Controllers;

/// <summary>
/// Prints an issued certificate for staff, revoked ones included, and previews a certification's template.
/// </summary>
[Feature(SkillsConstants.Features.IssuedCertificates)]
public sealed class IssuedCertificatesAdminController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly ICertificationRecordService _recordService;
    private readonly ICertificationManager _certificationManager;
    private readonly CertificateDocumentBuilder _documentBuilder;
    private readonly IClock _clock;

    public IssuedCertificatesAdminController(
        IAuthorizationService authorizationService,
        ICertificationRecordService recordService,
        ICertificationManager certificationManager,
        CertificateDocumentBuilder documentBuilder,
        IClock clock)
    {
        _authorizationService = authorizationService;
        _recordService = recordService;
        _certificationManager = certificationManager;
        _documentBuilder = documentBuilder;
        _clock = clock;
    }

    [Admin("certificates/document/{id}", "IssuedCertificateDocument")]
    public async Task<IActionResult> Document(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SkillsPermissions.ViewSkills))
        {
            return Forbid();
        }

        var record = await _recordService.FindByIdAsync(id, HttpContext.RequestAborted);

        if (record is null || string.IsNullOrEmpty(record.VerificationCode))
        {
            return NotFound();
        }

        var certification = await _certificationManager.FindByIdAsync(record.CertificationId);

        if (certification is null)
        {
            return NotFound();
        }

        var bytes = await _documentBuilder.BuildAsync(record, certification, VerificationUrl(record.VerificationCode));

        return File(bytes, "application/pdf");
    }

    [Admin("certifications/certificate-preview/{id}", "CertificationCertificatePreview")]
    public async Task<IActionResult> Preview(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SkillsPermissions.ManageSkills))
        {
            return Forbid();
        }

        var certification = await _certificationManager.FindByIdAsync(id);

        if (certification is null)
        {
            return NotFound();
        }

        var today = DateOnly.FromDateTime(_clock.UtcNow);
        var code = ReadableCodes.Generate(SkillsConstants.VerificationCodeLength);
        var prefix = string.IsNullOrWhiteSpace(certification.NumberPrefix) ? SkillsConstants.DefaultCertificateNumberPrefix : certification.NumberPrefix;
        var sample = new CertificationRecord
        {
            SubjectName = "Alex Sample",
            CertificationId = certification.ItemId,
            Number = certification.GenerateNumbers ? prefix + "000123" : null,
            IssuedOn = today,
            ExpiresOn = certification.ValidityMonths is { } months ? today.AddMonths(months).AddDays(-1) : null,
            Evidence = "Completed the course and passed the final assessment.",
            Status = CertificationStatus.Valid,
            VerificationCode = code,
        };

        var bytes = await _documentBuilder.BuildAsync(sample, certification, VerificationUrl(code));

        return File(bytes, "application/pdf");
    }

    private string VerificationUrl(string code)
    {
        return Url.Action("Show", "CertificateVerification", new { area = "CrestApps.OrchardCore.Skills", code = CertificateVerificationCodes.Format(code) }, Request.Scheme);
    }
}
