using CrestApps.OrchardCore.Skills;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Staff.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Staff.Controllers;

/// <summary>
/// Adds a certificate to a staff member from the Skills section of their profile. Only answers while Skills is on.
/// </summary>
public sealed class StaffCertificatesController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IStaffMemberManager _staffManager;
    private readonly IStaffScopeService _scopeService;
    private readonly INotifier _notifier;
    private readonly IClock _clock;

    internal readonly IHtmlLocalizer H;

    public StaffCertificatesController(
        IAuthorizationService authorizationService,
        IStaffMemberManager staffManager,
        IStaffScopeService scopeService,
        INotifier notifier,
        IClock clock,
        IHtmlLocalizer<StaffCertificatesController> htmlLocalizer)
    {
        _authorizationService = authorizationService;
        _staffManager = staffManager;
        _scopeService = scopeService;
        _notifier = notifier;
        _clock = clock;
        H = htmlLocalizer;
    }

    [HttpPost]
    [Admin("staff/certificates/add", "StaffAddCertificate")]
    public async Task<IActionResult> Add(AddStaffCertificateViewModel model)
    {
        // The certificate services come with the Skills feature.
        var recordService = HttpContext.RequestServices.GetService<ICertificationRecordService>();

        if (recordService is null)
        {
            return NotFound();
        }

        if (!await _authorizationService.AuthorizeAsync(User, SkillsPermissions.AssignSkills))
        {
            return Forbid();
        }

        var staff = string.IsNullOrEmpty(model.StaffMemberId) ? null : await _staffManager.FindByIdAsync(model.StaffMemberId);

        if (staff is null)
        {
            return NotFound();
        }

        if (!await _scopeService.CanManageAsync(User, staff, HttpContext.RequestAborted))
        {
            return Forbid();
        }

        var result = await recordService.SaveAsync(new CertificationRecord
        {
            SubjectKind = SkillSubjectKind.Staff,
            SubjectId = staff.ItemId,
            SubjectName = staff.DisplayName,
            CertificationId = model.CertificationId,
            Number = model.Number,
            IssuedOn = model.IssuedOn,
            ExpiresOn = model.ExpiresOn,
            Status = model.Checked ? CertificationStatus.Valid : CertificationStatus.Pending,
            VerifiedBy = model.Checked ? User.Identity?.Name : null,
            VerifiedUtc = model.Checked ? _clock.UtcNow : null,
        }, User.Identity?.Name, HttpContext.RequestAborted);

        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(H["The certificate has been added."]);
        }
        else
        {
            foreach (var error in result.Errors)
            {
                await _notifier.ErrorAsync(H["{0}", error]);
            }
        }

        return Redirect(Url.Action("Profile", "Staff", new { area = "CrestApps.OrchardCore.Staff", id = staff.ItemId }) + "#staff-skills");
    }
}
