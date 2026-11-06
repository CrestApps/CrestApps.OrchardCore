using CrestApps.OrchardCore.Core.Validation;
using CrestApps.OrchardCore.Skills;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using CrestApps.OrchardCore.Teams.Services;
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
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using YesSql;

namespace CrestApps.OrchardCore.Workforce.Controllers;

/// <summary>
/// The workers of the teams the user manages: the list and the editor, with the worker's certificates. The editor is
/// built by the display drivers of <see cref="WorkerProfile"/>, so other modules add sections to it, as Skills does.
/// </summary>
public sealed class WorkersController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IWorkerManager _workerManager;
    private readonly ITeamManager _teamManager;
    private readonly IWorkforceScopeService _scopeService;
    private readonly IDisplayManager<WorkerProfile> _displayManager;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly ISession _session;
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
    /// <param name="displayManager">The display manager that builds the worker editor.</param>
    /// <param name="updateModelAccessor">The accessor of the model updater the editor binds with.</param>
    /// <param name="session">The session, cancelled when a worker isn't saved.</param>
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
        IDisplayManager<WorkerProfile> displayManager,
        IUpdateModelAccessor updateModelAccessor,
        ISession session,
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
        _displayManager = displayManager;
        _updateModelAccessor = updateModelAccessor;
        _session = session;
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

        var isNew = string.IsNullOrEmpty(id);
        var worker = isNew
            ? await _workerManager.NewAsync()
            : await _workerManager.FindByIdAsync(id);

        if (worker is null)
        {
            return NotFound();
        }

        if (!isNew && !await _scopeService.ManagesWorkerAsync(User, worker, HttpContext.RequestAborted))
        {
            return Forbid();
        }

        if (isNew && !string.IsNullOrEmpty(teamId))
        {
            worker.TeamMemberships.Add(new TeamMembership { TeamId = teamId });
        }

        var model = new WorkerEditorPageViewModel
        {
            Id = id,
            DisplayName = worker.DisplayName,
            Editor = await _displayManager.BuildEditorAsync(worker, _updateModelAccessor.ModelUpdater, isNew),
        };

        await FillCertificatesAsync(model);

        return View(model);
    }

    /// <summary>
    /// Saves a worker, with the sections other modules add to the editor, such as their skills.
    /// </summary>
    /// <param name="id">The worker id, or empty for a new worker.</param>
    /// <returns>A redirect to the list, or the editor with errors.</returns>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("workforce/workers/edit/{id?}", "WorkersEdit")]
    public async Task<IActionResult> EditPost(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageWorkers))
        {
            return Forbid();
        }

        var isNew = string.IsNullOrEmpty(id);
        var worker = isNew
            ? await _workerManager.NewAsync()
            : await _workerManager.FindByIdAsync(id);

        if (worker is null)
        {
            return NotFound();
        }

        if (!isNew && !await _scopeService.ManagesWorkerAsync(User, worker, HttpContext.RequestAborted))
        {
            return Forbid();
        }

        var displayName = worker.DisplayName;
        var editor = await _displayManager.UpdateEditorAsync(worker, _updateModelAccessor.ModelUpdater, isNew);
        var isValid = await CatalogEntryValidation.ValidateAsync(_workerManager, worker, _updateModelAccessor.ModelUpdater, nameof(WorkerProfile), HttpContext.RequestAborted);

        if (!isValid || !ModelState.IsValid)
        {
            // A section may already have written records of its own, such as skills: none of it is kept.
            await _session.CancelAsync();

            var model = new WorkerEditorPageViewModel
            {
                Id = id,
                DisplayName = displayName,
                Editor = editor,
            };

            await FillCertificatesAsync(model);

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

    private async Task FillCertificatesAsync(WorkerEditorPageViewModel model)
    {
        model.CertificationOptions = (await _certificationManager.GetEnabledAsync(HttpContext.RequestAborted))
            .Select(certification => new SelectListItem(certification.Name, certification.ItemId))
            .ToList();

        if (string.IsNullOrEmpty(model.Id))
        {
            return;
        }

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
