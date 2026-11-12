using CrestApps.OrchardCore.Skills;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Staff.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Staff.Drivers;

/// <summary>
/// The Skills section of the staff profile: the skills the person holds today and their certificates, with a form to
/// add a certificate for those who manage them.
/// </summary>
internal sealed class StaffSkillsProfileDisplayDriver : DisplayDriver<StaffMember>
{
    private readonly ISkillManager _skillManager;
    private readonly ISkillAssignmentService _assignmentService;
    private readonly ICertificationManager _certificationManager;
    private readonly ICertificationRecordService _recordService;
    private readonly IStaffScopeService _scopeService;
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IClock _clock;

    public StaffSkillsProfileDisplayDriver(
        ISkillManager skillManager,
        ISkillAssignmentService assignmentService,
        ICertificationManager certificationManager,
        ICertificationRecordService recordService,
        IStaffScopeService scopeService,
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor,
        IClock clock)
    {
        _skillManager = skillManager;
        _assignmentService = assignmentService;
        _certificationManager = certificationManager;
        _recordService = recordService;
        _scopeService = scopeService;
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
        _clock = clock;
    }

    public override IDisplayResult Display(StaffMember staff, BuildDisplayContext context)
    {
        if (context.DisplayType != StaffProfileSections.DisplayType || string.IsNullOrEmpty(staff.ItemId))
        {
            return null;
        }

        return Initialize<StaffSkillsProfileViewModel>("StaffProfileSkills", async model =>
        {
            var subject = SkillSubject.Staff(staff.ItemId);
            var skills = (await _skillManager.GetAllAsync()).ToDictionary(skill => skill.ItemId, skill => skill.Name, StringComparer.Ordinal);
            var certifications = (await _certificationManager.GetAllAsync()).ToDictionary(certification => certification.ItemId, certification => certification.Name, StringComparer.Ordinal);
            var principal = _httpContextAccessor.HttpContext?.User;

            model.StaffMemberId = staff.ItemId;
            model.Skills = [.. (await _assignmentService.GetEffectiveAsync(subject, DateOnly.FromDateTime(_clock.UtcNow)))
                .Select(assignment => new StaffSkillRowViewModel
                {
                    Name = skills.GetValueOrDefault(assignment.SkillId ?? string.Empty, assignment.SkillId),
                    Proficiency = assignment.Proficiency,
                })
                .OrderBy(row => row.Name, StringComparer.CurrentCultureIgnoreCase)];
            model.Certificates = [.. (await _recordService.GetForSubjectAsync(subject))
                .Select(record => new StaffCertificateRowViewModel
                {
                    Id = record.ItemId,
                    CertificationName = certifications.GetValueOrDefault(record.CertificationId ?? string.Empty),
                    Number = record.Number,
                    ExpiresOn = record.ExpiresOn,
                    Status = record.Status.ToString(),
                })];
            model.CanAdd = principal is not null
                && await _authorizationService.AuthorizeAsync(principal, SkillsPermissions.AssignSkills)
                && await _scopeService.CanManageAsync(principal, staff);
            model.CertificationOptions = model.CanAdd
                ? [.. (await _certificationManager.GetEnabledAsync()).Select(certification => new SelectListItem(certification.Name, certification.ItemId))]
                : [];
        }).Location(StaffProfileSections.DisplayType, StaffProfileSections.Location(StaffProfileSections.Skills, "1"));
    }
}
