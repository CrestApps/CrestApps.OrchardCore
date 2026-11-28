using System.Text;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Surveys.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using QRCoder;

namespace CrestApps.OrchardCore.Surveys.Controllers;

/// <summary>
/// The collectors of a survey: public links and kiosks with their windows, limits, passwords and variables, and their
/// QR codes.
/// </summary>
[Admin]
public sealed class SurveyCollectorsController : Controller
{
    private readonly ISurveyManager _manager;
    private readonly SurveyCollectorStore _collectorStore;
    private readonly SurveyCollectorManager _collectorManager;
    private readonly SurveyLinkService _linkService;
    private readonly ISurveyAuthorizationService _surveyAuthorization;
    private readonly ILocalClock _localClock;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyCollectorsController"/> class.
    /// </summary>
    public SurveyCollectorsController(
        ISurveyManager manager,
        SurveyCollectorStore collectorStore,
        SurveyCollectorManager collectorManager,
        SurveyLinkService linkService,
        ISurveyAuthorizationService surveyAuthorization,
        ILocalClock localClock,
        INotifier notifier,
        IHtmlLocalizer<SurveyCollectorsController> htmlLocalizer,
        IStringLocalizer<SurveyCollectorsController> stringLocalizer)
    {
        _manager = manager;
        _collectorStore = collectorStore;
        _collectorManager = collectorManager;
        _linkService = linkService;
        _surveyAuthorization = surveyAuthorization;
        _localClock = localClock;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists the collectors of a survey.
    /// </summary>
    /// <param name="id">The survey.</param>
    /// <returns>The page.</returns>
    [Admin("surveys/{id}/collectors", "SurveysCollectors")]
    public async Task<IActionResult> Index(string id)
    {
        var survey = await AuthorizeSurveyAsync(id);

        if (survey is null)
        {
            return Forbid();
        }

        return View(new SurveyCollectorsViewModel
        {
            Survey = survey,
            List = new AdminListContext<SurveyCollector> { Source = await _collectorStore.ListBySurveyAsync(id, HttpContext.RequestAborted) },
        });
    }

    /// <summary>
    /// Shows the new collector form.
    /// </summary>
    /// <param name="id">The survey.</param>
    /// <param name="kind">The kind.</param>
    /// <returns>The page.</returns>
    [Admin("surveys/{id}/collectors/create", "SurveysCollectorsCreate")]
    public async Task<IActionResult> Create(string id, string kind = SurveyCollectorKinds.WebLink)
    {
        var survey = await AuthorizeSurveyAsync(id);

        if (survey is null)
        {
            return Forbid();
        }

        return View("Edit", new SurveyCollectorEditViewModel
        {
            Survey = survey,
            Kind = kind == SurveyCollectorKinds.Kiosk ? SurveyCollectorKinds.Kiosk : SurveyCollectorKinds.WebLink,
            Name = kind == SurveyCollectorKinds.Kiosk ? S["Kiosk"] : S["Web link"],
        });
    }

    /// <summary>
    /// Creates a collector.
    /// </summary>
    /// <param name="id">The survey.</param>
    /// <param name="model">The form.</param>
    /// <returns>The page, or a redirect.</returns>
    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("surveys/{id}/collectors/create", "SurveysCollectorsCreate")]
    public async Task<IActionResult> CreatePost(string id, SurveyCollectorEditViewModel model)
    {
        var survey = await AuthorizeSurveyAsync(id);

        if (survey is null)
        {
            return Forbid();
        }

        var collector = _collectorManager.New(id, model.Kind == SurveyCollectorKinds.Kiosk ? SurveyCollectorKinds.Kiosk : SurveyCollectorKinds.WebLink);

        return await SaveAsync(survey, collector, model, isNew: true);
    }

    /// <summary>
    /// Shows a collector.
    /// </summary>
    /// <param name="collectorId">The collector.</param>
    /// <returns>The page.</returns>
    [Admin("surveys/collectors/{collectorId}", "SurveysCollectorsEdit")]
    public async Task<IActionResult> Edit(string collectorId)
    {
        var (survey, collector) = await AuthorizeCollectorAsync(collectorId);

        if (collector is null)
        {
            return Forbid();
        }

        return View(new SurveyCollectorEditViewModel
        {
            Survey = survey,
            Collector = collector,
            CollectorId = collector.ItemId,
            Name = collector.Name,
            Kind = collector.Kind,
            Opens = await LocalAsync(collector.OpensUtc),
            Closes = await LocalAsync(collector.ClosesUtc),
            MaxResponses = collector.MaxResponses,
            Variables = string.Join(Environment.NewLine, collector.Variables.Select(pair => $"{pair.Key}={pair.Value}")),
            Culture = collector.Culture,
            KioskResetSeconds = collector.KioskResetSeconds,
            HasPassword = !string.IsNullOrEmpty(collector.PasswordHash),
            Link = _linkService.BuildLink(collector.Token),
        });
    }

    /// <summary>
    /// Saves a collector.
    /// </summary>
    /// <param name="collectorId">The collector.</param>
    /// <param name="model">The form.</param>
    /// <returns>The page, or a redirect.</returns>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("surveys/collectors/{collectorId}", "SurveysCollectorsEdit")]
    public async Task<IActionResult> EditPost(string collectorId, SurveyCollectorEditViewModel model)
    {
        var (survey, collector) = await AuthorizeCollectorAsync(collectorId);

        if (collector is null)
        {
            return Forbid();
        }

        return await SaveAsync(survey, collector, model, isNew: false);
    }

    /// <summary>
    /// Opens or closes a collector.
    /// </summary>
    /// <param name="collectorId">The collector.</param>
    /// <param name="status">The new status.</param>
    /// <returns>A redirect to the list.</returns>
    [HttpPost]
    [Admin("surveys/collectors/{collectorId}/status", "SurveysCollectorsStatus")]
    public async Task<IActionResult> Status(string collectorId, CollectorStatus status)
    {
        var (survey, collector) = await AuthorizeCollectorAsync(collectorId);

        if (collector is null)
        {
            return Forbid();
        }

        collector.Status = status;
        await _collectorManager.SaveAsync(collector, isNew: false, HttpContext.RequestAborted);
        await _notifier.SuccessAsync(status == CollectorStatus.Open ? H["The collector is open."] : H["The collector is closed."]);

        return RedirectToAction(nameof(Index), new { id = survey.ItemId });
    }

    /// <summary>
    /// Returns the QR code of a collector's link as SVG.
    /// </summary>
    /// <param name="collectorId">The collector.</param>
    /// <returns>The SVG file.</returns>
    [Admin("surveys/collectors/{collectorId}/qr.svg", "SurveysCollectorsQr")]
    public async Task<IActionResult> Qr(string collectorId)
    {
        var (_, collector) = await AuthorizeCollectorAsync(collectorId);

        if (collector is null)
        {
            return Forbid();
        }

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(_linkService.BuildLink(collector.Token), QRCodeGenerator.ECCLevel.M);
        var svg = new SvgQRCode(data).GetGraphic(8);

        return File(Encoding.UTF8.GetBytes(svg), "image/svg+xml", $"survey-{collector.Token}.svg");
    }

    private async Task<IActionResult> SaveAsync(Survey survey, SurveyCollector collector, SurveyCollectorEditViewModel model, bool isNew)
    {
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in (model.Variables ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = line.IndexOf('=');

            if (separator <= 0 || !SurveyNames.IsValidVariableCode(line.Substring(0, separator).Trim()))
            {
                ModelState.AddModelError(nameof(model.Variables), S["Write each variable as name=value on its own line."]);

                break;
            }

            variables[line.Substring(0, separator).Trim()] = line.Substring(separator + 1).Trim();
        }

        var opens = await UtcAsync(model.Opens);
        var closes = await UtcAsync(model.Closes);

        if (opens is not null && closes is not null && closes <= opens)
        {
            ModelState.AddModelError(nameof(model.Closes), S["The collector must close after it opens."]);
        }

        if (!ModelState.IsValid)
        {
            model.Survey = survey;
            model.Collector = isNew ? null : collector;
            model.HasPassword = !string.IsNullOrEmpty(collector.PasswordHash);
            model.Link = isNew ? null : _linkService.BuildLink(collector.Token);

            return View("Edit", model);
        }

        collector.Name = model.Name?.Trim();
        collector.OpensUtc = opens;
        collector.ClosesUtc = closes;
        collector.MaxResponses = model.MaxResponses is > 0 ? model.MaxResponses : null;
        collector.Variables = variables;
        collector.Culture = string.IsNullOrWhiteSpace(model.Culture) ? null : model.Culture.Trim();
        collector.KioskResetSeconds = Math.Clamp(model.KioskResetSeconds, 3, 300);

        if (model.RemovePassword)
        {
            SurveyCollectorManager.SetPassword(collector, null);
        }
        else if (!string.IsNullOrEmpty(model.Password))
        {
            SurveyCollectorManager.SetPassword(collector, model.Password);
        }

        await _collectorManager.SaveAsync(collector, isNew, HttpContext.RequestAborted);
        await _notifier.SuccessAsync(H["The collector has been saved."]);

        return RedirectToAction(nameof(Index), new { id = survey.ItemId });
    }

    private async Task<Survey> AuthorizeSurveyAsync(string id)
    {
        var survey = string.IsNullOrEmpty(id) ? null : await _manager.FindByIdAsync(id);

        return survey is not null && await _surveyAuthorization.AuthorizeAsync(User, survey, SurveyOperation.Manage, HttpContext.RequestAborted) ? survey : null;
    }

    private async Task<(Survey Survey, SurveyCollector Collector)> AuthorizeCollectorAsync(string collectorId)
    {
        var collector = string.IsNullOrEmpty(collectorId) ? null : await _collectorStore.FindByIdAsync(collectorId);
        var survey = collector is null ? null : await AuthorizeSurveyAsync(collector.SurveyId);

        // An invitation collector (its link, dates, password and status) is the invitations': changing it here takes the
        // right to manage the survey's invitations too, as the invitations pages do.
        if (survey is not null
            && collector.Kind == SurveyCollectorKinds.Invitation
            && !await _surveyAuthorization.AuthorizeAsync(User, survey, SurveyOperation.ManageInvitations, HttpContext.RequestAborted))
        {
            return (null, null);
        }

        return survey is null ? (null, null) : (survey, collector);
    }

    private async Task<DateTime?> LocalAsync(DateTime? utc)
        => utc is { } value ? (await _localClock.ConvertToLocalAsync(DateTime.SpecifyKind(value, DateTimeKind.Utc))).DateTime : null;

    private async Task<DateTime?> UtcAsync(DateTime? local)
        => local is { } value ? await _localClock.ToUtcLenientAsync(DateTime.SpecifyKind(value, DateTimeKind.Unspecified)) : null;
}
