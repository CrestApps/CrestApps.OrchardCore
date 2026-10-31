using CrestApps.OrchardCore.Skills;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using CrestApps.OrchardCore.Workforce.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using CrestApps.OrchardCore.Workforce.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Workforce.Controllers;

/// <summary>
/// The workers of the teams the user manages: the list and the editor, with the worker's teams, availability, skills
/// and certificates.
/// </summary>
public sealed class WorkersController : Controller
{
    private const int EmptyMembershipRows = 3;

    private readonly IAuthorizationService _authorizationService;
    private readonly IWorkerManager _workerManager;
    private readonly ITeamManager _teamManager;
    private readonly IWorkforceScopeService _scopeService;
    private readonly ISkillManager _skillManager;
    private readonly ISkillAssignmentService _assignmentService;
    private readonly ICertificationManager _certificationManager;
    private readonly ICertificationRecordService _recordService;
    private readonly IClock _clock;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkersController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="workerManager">The worker manager.</param>
    /// <param name="teamManager">The team manager.</param>
    /// <param name="scopeService">The workforce scope service.</param>
    /// <param name="skillManager">The skill manager.</param>
    /// <param name="assignmentService">The skill assignment service.</param>
    /// <param name="certificationManager">The certification manager.</param>
    /// <param name="recordService">The certification record service.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public WorkersController(
        IAuthorizationService authorizationService,
        IWorkerManager workerManager,
        ITeamManager teamManager,
        IWorkforceScopeService scopeService,
        ISkillManager skillManager,
        ISkillAssignmentService assignmentService,
        ICertificationManager certificationManager,
        ICertificationRecordService recordService,
        IClock clock,
        INotifier notifier,
        IHtmlLocalizer<WorkersController> htmlLocalizer,
        IStringLocalizer<WorkersController> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _workerManager = workerManager;
        _teamManager = teamManager;
        _scopeService = scopeService;
        _skillManager = skillManager;
        _assignmentService = assignmentService;
        _certificationManager = certificationManager;
        _recordService = recordService;
        _clock = clock;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists the workers of the teams the user manages.
    /// </summary>
    /// <param name="model">The filters.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The list.</returns>
    [Admin("workforce/workers", "Workers")]
    public async Task<IActionResult> Index(WorkforceListViewModel model, PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ViewWorkers))
        {
            return Forbid();
        }

        var scope = await _scopeService.GetManagedScopeAsync(User, HttpContext.RequestAborted);
        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var query = new WorkerQuery
        {
            Search = model.Search,
            TeamId = string.IsNullOrEmpty(model.TeamId) ? null : model.TeamId,
            Status = Enum.TryParse<WorkerStatus>(model.Status, out var status) ? status : null,
            TeamIds = scope.All ? null : scope.TeamIds.ToArray(),
        };

        var (count, workers) = await _workerManager.SearchAsync(query, pager.GetStartIndex(), pager.PageSize, HttpContext.RequestAborted);

        model.List = new AdminListContext<WorkerProfile>
        {
            Source = workers.ToList(),
            Pager = await shapeFactory.PagerAsync(pager, count, new RouteValueDictionary
            {
                { "search", model.Search },
                { "teamId", model.TeamId },
                { "status", model.Status },
            }),
        };

        model.TeamOptions = await TeamOptionsAsync(scope, model.TeamId);
        model.StatusOptions = StatusOptions(model.Status);

        return View(model);
    }

    /// <summary>
    /// Shows the editor of a new or existing worker.
    /// </summary>
    /// <param name="id">The worker id, or empty for a new worker.</param>
    /// <param name="teamId">The team a new worker joins.</param>
    /// <returns>The editor.</returns>
    [Admin("workforce/workers/edit/{id?}", "WorkersEdit")]
    public async Task<IActionResult> Edit(string id, string teamId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageWorkers))
        {
            return Forbid();
        }

        var worker = string.IsNullOrEmpty(id)
            ? await _workerManager.NewAsync()
            : await _workerManager.FindByIdAsync(id);

        if (worker is null)
        {
            return NotFound();
        }

        if (!string.IsNullOrEmpty(id) && !await _scopeService.ManagesWorkerAsync(User, worker, HttpContext.RequestAborted))
        {
            return Forbid();
        }

        if (string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(teamId))
        {
            worker.TeamMemberships.Add(new TeamMembership { TeamId = teamId });
        }

        var model = new WorkerEditViewModel
        {
            Id = id,
            DisplayName = worker.DisplayName,
            UserId = worker.UserId,
            Email = worker.Email,
            Phone = worker.Phone,
            EmployeeNumber = worker.EmployeeNumber,
            ExternalId = worker.ExternalId,
            JobTitle = worker.JobTitle,
            EmploymentType = worker.EmploymentType.ToString(),
            Status = worker.Status.ToString(),
            HireDate = worker.HireDate,
            SeniorityDate = worker.SeniorityDate,
            TerminationDate = worker.TerminationDate,
            TimeZoneId = worker.TimeZoneId,
            HomeBase = worker.HomeBase?.Address,
            ContractedHoursPerWeek = worker.ContractedHoursPerWeek,
            MinHoursPerWeek = worker.MinHoursPerWeek,
            MaxHoursPerWeek = worker.MaxHoursPerWeek,
            MaxHoursPerDay = worker.MaxHoursPerDay,
            Availability = AvailabilityFormat.Write(worker.Availability),
            Memberships = worker.TeamMemberships.Select(membership => new TeamMembershipRowViewModel
            {
                TeamId = membership.TeamId,
                Role = membership.Role.ToString(),
                EffectiveFrom = membership.EffectiveFrom,
                EffectiveTo = membership.EffectiveTo,
            }).ToList(),
        };

        model.CanSeeSensitive = await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ViewSensitiveWorkerData);

        if (model.CanSeeSensitive)
        {
            model.DateOfBirth = worker.DateOfBirth;
        }

        var held = string.IsNullOrEmpty(id)
            ? new Dictionary<string, SkillAssignment>(StringComparer.Ordinal)
            : (await _assignmentService.GetAssignmentsAsync(SkillSubject.Worker(id), HttpContext.RequestAborted)).ToDictionary(assignment => assignment.SkillId, StringComparer.Ordinal);

        model.Skills = (await _skillManager.GetEnabledAsync(HttpContext.RequestAborted)).Select(skill => new WorkerSkillRowViewModel
        {
            SkillId = skill.ItemId,
            Name = skill.Name,
            Proficiency = held.TryGetValue(skill.ItemId, out var assignment) ? assignment.Proficiency : 0,
        }).ToList();

        await FillAsync(model);

        return View(model);
    }

    /// <summary>
    /// Saves a worker, with their skills.
    /// </summary>
    /// <param name="model">The fields.</param>
    /// <returns>A redirect to the list, or the editor with errors.</returns>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("workforce/workers/edit/{id?}", "WorkersEdit")]
    public async Task<IActionResult> EditPost(WorkerEditViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageWorkers))
        {
            return Forbid();
        }

        var isNew = string.IsNullOrEmpty(model.Id);
        var worker = isNew
            ? await _workerManager.NewAsync()
            : await _workerManager.FindByIdAsync(model.Id);

        if (worker is null)
        {
            return NotFound();
        }

        var scope = await _scopeService.GetManagedScopeAsync(User, HttpContext.RequestAborted);

        if (!isNew && !await _scopeService.ManagesWorkerAsync(User, worker, HttpContext.RequestAborted))
        {
            return Forbid();
        }

        var availabilityErrors = new List<string>();
        var availability = AvailabilityFormat.Read(model.Availability, availabilityErrors);

        foreach (var error in availabilityErrors)
        {
            ModelState.AddModelError(nameof(model.Availability), S["This line can't be read: {0}", error]);
        }

        var memberships = (model.Memberships ?? [])
            .Where(row => !string.IsNullOrEmpty(row.TeamId))
            .Select(row => new TeamMembership
            {
                TeamId = row.TeamId,
                Role = Enum.TryParse<TeamMemberRole>(row.Role, out var role) ? role : TeamMemberRole.Member,
                EffectiveFrom = row.EffectiveFrom,
                EffectiveTo = row.EffectiveTo,
            })
            .ToList();

        // A manager can only place workers in the teams they manage.
        if (!scope.All && memberships.Any(membership => !scope.Covers(membership.TeamId)))
        {
            ModelState.AddModelError(nameof(model.Memberships), S["You can only add workers to the teams you manage."]);
        }

        worker.DisplayName = model.DisplayName;
        worker.UserId = model.UserId;
        worker.Email = model.Email;
        worker.Phone = model.Phone;
        worker.EmployeeNumber = model.EmployeeNumber;
        worker.ExternalId = model.ExternalId;
        worker.JobTitle = model.JobTitle;
        worker.EmploymentType = Enum.TryParse<EmploymentType>(model.EmploymentType, out var employmentType) ? employmentType : EmploymentType.FullTime;
        worker.Status = Enum.TryParse<WorkerStatus>(model.Status, out var status) ? status : WorkerStatus.Active;
        worker.HireDate = model.HireDate;
        worker.SeniorityDate = model.SeniorityDate;
        worker.TerminationDate = model.TerminationDate;
        worker.TimeZoneId = model.TimeZoneId;
        worker.HomeBase = string.IsNullOrWhiteSpace(model.HomeBase) ? null : new WorkforceLocation { Address = model.HomeBase.Trim(), Latitude = worker.HomeBase?.Latitude, Longitude = worker.HomeBase?.Longitude };
        worker.ContractedHoursPerWeek = model.ContractedHoursPerWeek;
        worker.MinHoursPerWeek = model.MinHoursPerWeek;
        worker.MaxHoursPerWeek = model.MaxHoursPerWeek;
        worker.MaxHoursPerDay = model.MaxHoursPerDay;
        worker.Availability = availability;
        worker.TeamMemberships = memberships;
        worker.PrimaryTeamId = memberships.FirstOrDefault()?.TeamId;

        if (await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ViewSensitiveWorkerData))
        {
            worker.DateOfBirth = model.DateOfBirth;
        }

        var validation = await _workerManager.ValidateAsync(worker);

        foreach (var error in validation.Errors)
        {
            ModelState.AddModelError(error.MemberNames.FirstOrDefault() ?? string.Empty, error.ErrorMessage);
        }

        if (!ModelState.IsValid)
        {
            model.CanSeeSensitive = await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ViewSensitiveWorkerData);
            await FillAsync(model);

            return View(model);
        }

        if (isNew)
        {
            await _workerManager.CreateAsync(worker);
        }
        else
        {
            await _workerManager.UpdateAsync(worker);
        }

        await SaveSkillsAsync(worker, model.Skills);
        await _notifier.SuccessAsync(H["The worker has been saved."]);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Adds a certificate to a worker.
    /// </summary>
    /// <param name="model">The certificate.</param>
    /// <returns>A redirect to the worker.</returns>
    [HttpPost]
    [Admin("workforce/workers/certificates/add", "WorkersAddCertificate")]
    public async Task<IActionResult> AddCertificate(AddWorkerCertificateViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageWorkers)
            || !await _authorizationService.AuthorizeAsync(User, SkillsPermissions.AssignSkills))
        {
            return Forbid();
        }

        var worker = string.IsNullOrEmpty(model.WorkerId) ? null : await _workerManager.FindByIdAsync(model.WorkerId);

        if (worker is null)
        {
            return NotFound();
        }

        if (!await _scopeService.ManagesWorkerAsync(User, worker, HttpContext.RequestAborted))
        {
            return Forbid();
        }

        var result = await _recordService.SaveAsync(new CertificationRecord
        {
            SubjectKind = SkillSubjectKind.Worker,
            SubjectId = worker.ItemId,
            SubjectName = worker.DisplayName,
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

        return RedirectToAction(nameof(Edit), new { id = worker.ItemId });
    }

    /// <summary>
    /// Deletes a worker. Their skills and certificates are kept.
    /// </summary>
    /// <param name="id">The worker id.</param>
    /// <returns>A redirect to the list.</returns>
    [HttpPost]
    [Admin("workforce/workers/delete/{id}", "WorkersDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageWorkers))
        {
            return Forbid();
        }

        var worker = await _workerManager.FindByIdAsync(id);

        if (worker is not null)
        {
            if (!await _scopeService.ManagesWorkerAsync(User, worker, HttpContext.RequestAborted))
            {
                return Forbid();
            }

            await _workerManager.DeleteAsync(worker);
            await _notifier.SuccessAsync(H["The worker has been deleted."]);
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task SaveSkillsAsync(WorkerProfile worker, IList<WorkerSkillRowViewModel> rows)
    {
        if (rows is null || !await _authorizationService.AuthorizeAsync(User, SkillsPermissions.AssignSkills))
        {
            return;
        }

        var subject = SkillSubject.Worker(worker.ItemId);
        var held = (await _assignmentService.GetAssignmentsAsync(subject, HttpContext.RequestAborted)).ToDictionary(assignment => assignment.SkillId, StringComparer.Ordinal);
        var enabled = (await _skillManager.GetEnabledAsync(HttpContext.RequestAborted)).Select(skill => skill.ItemId).ToHashSet(StringComparer.Ordinal);

        foreach (var row in rows.Where(row => !string.IsNullOrEmpty(row.SkillId) && enabled.Contains(row.SkillId)))
        {
            held.TryGetValue(row.SkillId, out var existing);

            if (!SkillProficiency.IsValid(row.Proficiency))
            {
                if (existing is not null)
                {
                    await _assignmentService.RemoveAsync(subject, row.SkillId, User.Identity?.Name, HttpContext.RequestAborted);
                }

                continue;
            }

            await _assignmentService.AssignAsync(subject, new SkillAssignmentInput
            {
                SkillId = row.SkillId,
                Proficiency = row.Proficiency,
                EffectiveFrom = existing?.EffectiveFrom,
                EffectiveTo = existing?.EffectiveTo,
            }, User.Identity?.Name, HttpContext.RequestAborted);
        }
    }

    private async Task FillAsync(WorkerEditViewModel model)
    {
        var scope = await _scopeService.GetManagedScopeAsync(User, HttpContext.RequestAborted);

        while (model.Memberships.Count(row => string.IsNullOrEmpty(row.TeamId)) < EmptyMembershipRows)
        {
            model.Memberships.Add(new TeamMembershipRowViewModel { Role = nameof(TeamMemberRole.Member) });
        }

        model.TeamOptions = await TeamOptionsAsync(scope, null);
        model.EmploymentTypeOptions =
        [
            new SelectListItem(S["Full time"], nameof(EmploymentType.FullTime), model.EmploymentType == nameof(EmploymentType.FullTime)),
            new SelectListItem(S["Part time"], nameof(EmploymentType.PartTime), model.EmploymentType == nameof(EmploymentType.PartTime)),
            new SelectListItem(S["Seasonal"], nameof(EmploymentType.Seasonal), model.EmploymentType == nameof(EmploymentType.Seasonal)),
            new SelectListItem(S["Temporary"], nameof(EmploymentType.Temporary), model.EmploymentType == nameof(EmploymentType.Temporary)),
            new SelectListItem(S["Contractor"], nameof(EmploymentType.Contractor), model.EmploymentType == nameof(EmploymentType.Contractor)),
            new SelectListItem(S["Subcontractor"], nameof(EmploymentType.Subcontractor), model.EmploymentType == nameof(EmploymentType.Subcontractor)),
        ];
        model.StatusOptions = StatusOptions(model.Status);
        model.TimeZoneOptions = _clock.GetTimeZones().Select(zone => new SelectListItem(zone.TimeZoneId, zone.TimeZoneId, zone.TimeZoneId == model.TimeZoneId)).ToList();
        model.CertificationOptions = (await _certificationManager.GetEnabledAsync(HttpContext.RequestAborted))
            .Select(certification => new SelectListItem(certification.Name, certification.ItemId))
            .ToList();

        if (!string.IsNullOrEmpty(model.Id))
        {
            var certifications = (await _certificationManager.GetAllAsync(HttpContext.RequestAborted)).ToDictionary(certification => certification.ItemId, certification => certification.Name, StringComparer.Ordinal);

            model.Certificates = (await _recordService.GetForSubjectAsync(SkillSubject.Worker(model.Id), HttpContext.RequestAborted))
                .Select(record => new WorkerCertificateViewModel
                {
                    Id = record.ItemId,
                    CertificationName = certifications.GetValueOrDefault(record.CertificationId ?? string.Empty),
                    Number = record.Number,
                    ExpiresOn = record.ExpiresOn,
                    Status = record.Status.ToString(),
                })
                .ToList();
        }
    }

    private async Task<IList<SelectListItem>> TeamOptionsAsync(WorkforceScope scope, string selected)
    {
        return (await _teamManager.ListTreeAsync(HttpContext.RequestAborted))
            .Where(item => scope.Covers(item.Team.ItemId))
            .Select(item => new SelectListItem(new string(' ', item.Depth * 4) + item.Team.Name, item.Team.ItemId, item.Team.ItemId == selected))
            .ToList();
    }

    private IList<SelectListItem> StatusOptions(string selected)
    {
        return
        [
            new SelectListItem(S["Active"], nameof(WorkerStatus.Active), selected == nameof(WorkerStatus.Active)),
            new SelectListItem(S["On leave"], nameof(WorkerStatus.OnLeave), selected == nameof(WorkerStatus.OnLeave)),
            new SelectListItem(S["Suspended"], nameof(WorkerStatus.Suspended), selected == nameof(WorkerStatus.Suspended)),
            new SelectListItem(S["Terminated"], nameof(WorkerStatus.Terminated), selected == nameof(WorkerStatus.Terminated)),
        ];
    }
}
