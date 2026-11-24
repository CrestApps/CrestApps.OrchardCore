using System.Security.Claims;
using CrestApps.OrchardCore.Learning.Certifications.Core.Services;
using CrestApps.OrchardCore.Learning.Certifications.Models;
using CrestApps.OrchardCore.Learning.Certifications.Services;
using CrestApps.OrchardCore.Learning.Certifications.ViewModels;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Certifications.Controllers;

/// <summary>
/// The signed-in learner's certifications: where they stand on each path, the certificate with its verification page,
/// and the courses that renew it.
/// </summary>
[Authorize]
public sealed class MyCertificationsController : Controller
{
    private readonly ICertificationPathService _paths;
    private readonly ICertificationRecordService _records;
    private readonly ICertificationManager _certifications;
    private readonly IContentManager _contentManager;

    public MyCertificationsController(
        ICertificationPathService paths,
        ICertificationRecordService records,
        ICertificationManager certifications,
        IContentManager contentManager)
    {
        _paths = paths;
        _records = records;
        _certifications = certifications;
        _contentManager = contentManager;
    }

    [HttpGet("learning/certifications")]
    public async Task<IActionResult> Index()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var model = new MyCertificationsViewModel();

        foreach (var progress in await _paths.ListProgressForUserAsync(userId, HttpContext.RequestAborted))
        {
            var certification = await _certifications.FindByIdAsync(progress.CertificationId);
            var record = string.IsNullOrEmpty(progress.CurrentRecordId) ? null : await _records.FindByIdAsync(progress.CurrentRecordId, HttpContext.RequestAborted);
            var entry = new MyCertificationEntry
            {
                Progress = progress,
                CertificationName = certification?.Name,
                Number = record?.Number,
                VerificationCode = record?.Status == CertificationStatus.Revoked ? null : CertificateVerificationCodes.Format(record?.VerificationCode),
                IssuedOn = record?.IssuedOn,
            };

            if (progress.State == CertificationPathState.RenewalOpen)
            {
                var path = await _paths.FindByIdAsync(progress.PathId, HttpContext.RequestAborted);

                foreach (var requirement in path?.RequirementsFor(renewal: true) ?? [])
                {
                    var target = await _contentManager.GetAsync(requirement.TargetId);

                    if (target is not null && target.ContentType == LearningConstants.ContentTypes.Course)
                    {
                        entry.RenewalCourses.Add(new MyCertificationCourse { CourseId = target.ContentItemId, Title = target.DisplayText });
                    }
                }
            }

            model.Entries.Add(entry);
        }

        return View(model);
    }
}
