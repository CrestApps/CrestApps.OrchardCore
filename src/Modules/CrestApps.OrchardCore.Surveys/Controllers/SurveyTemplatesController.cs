using System.Text.Json;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Surveys.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Routing;

namespace CrestApps.OrchardCore.Surveys.Controllers;

/// <summary>
/// Lists, adds, changes and removes the templates new surveys start from.
/// </summary>
public sealed class SurveyTemplatesController : Controller
{
    private static readonly JsonSerializerOptions _indented = new(CatalogDeploymentSerializer.Options) { WriteIndented = true };

    private readonly SurveyTemplateStore _store;
    private readonly SurveyStore _surveys;
    private readonly IAuthorizationService _authorizationService;
    private readonly ISurveyAuthorizationService _surveyAuthorization;
    private readonly IShapeFactory _shapeFactory;
    private readonly INotifier _notifier;
    private readonly IClock _clock;
    private readonly PagerOptions _pagerOptions;
    private readonly SurveyAccessScopes _scopes;
    private readonly SurveyIndexReader _reader;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyTemplatesController"/> class.
    /// </summary>
    /// <param name="store">The template store.</param>
    /// <param name="surveys">The survey store.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="surveyAuthorization">The survey authorization service.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    /// <param name="scopes">Works out the surveys a user may manage.</param>
    /// <param name="reader">Reads surveys from the survey index.</param>
    public SurveyTemplatesController(
        SurveyTemplateStore store,
        SurveyStore surveys,
        IAuthorizationService authorizationService,
        ISurveyAuthorizationService surveyAuthorization,
        IShapeFactory shapeFactory,
        INotifier notifier,
        IClock clock,
        IOptions<PagerOptions> pagerOptions,
        IHtmlLocalizer<SurveyTemplatesController> htmlLocalizer,
        IStringLocalizer<SurveyTemplatesController> stringLocalizer,
        SurveyAccessScopes scopes,
        SurveyIndexReader reader)
    {
        _store = store;
        _surveys = surveys;
        _authorizationService = authorizationService;
        _surveyAuthorization = surveyAuthorization;
        _shapeFactory = shapeFactory;
        _notifier = notifier;
        _clock = clock;
        _pagerOptions = pagerOptions.Value;
        _scopes = scopes;
        _reader = reader;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists the templates.
    /// </summary>
    /// <param name="search">Text the title or name contains.</param>
    /// <param name="pagerParameters">The page.</param>
    /// <returns>The page.</returns>
    [Admin("surveys/templates", "SurveyTemplates")]
    public async Task<IActionResult> Index(string search, PagerParameters pagerParameters)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SurveysPermissions.ManageSurveyTemplates))
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, _pagerOptions.GetPageSize());
        var all = (await _store.ListOrderedAsync(HttpContext.RequestAborted))
            .Where(template => string.IsNullOrWhiteSpace(search)
                || (template.Title?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
                || (template.Name?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false))
            .ToList();
        var routeData = new RouteData();

        if (!string.IsNullOrWhiteSpace(search))
        {
            routeData.Values.Add("search", search);
        }

        return View(new SurveyTemplateIndexViewModel
        {
            Search = search,
            List = new AdminListContext<SurveyTemplate>
            {
                Source = all.Skip(pager.GetStartIndex()).Take(pager.PageSize).ToList(),
                Pager = await _shapeFactory.PagerAsync(pager, all.Count, routeData),
            },
        });
    }

    /// <summary>
    /// Applies the search of the list.
    /// </summary>
    /// <param name="model">The posted list.</param>
    /// <returns>A redirect to the filtered list.</returns>
    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("surveys/templates", "SurveyTemplates")]
    public IActionResult IndexFilter(SurveyTemplateIndexViewModel model)
        => RedirectToAction(nameof(Index), new { search = model?.Search });

    /// <summary>
    /// Shows the form of a new template, copying a survey's draft when one is given.
    /// </summary>
    /// <param name="fromSurveyId">The survey to copy, or <see langword="null"/>.</param>
    /// <returns>The page.</returns>
    [Admin("surveys/templates/create", "SurveyTemplatesCreate")]
    public async Task<IActionResult> Create(string fromSurveyId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SurveysPermissions.ManageSurveyTemplates))
        {
            return Forbid();
        }

        var model = new SurveyTemplateEditViewModel { FromSurveyId = fromSurveyId };
        var survey = await FindCopyableSurveyAsync(fromSurveyId);

        if (survey is not null)
        {
            model.Title = survey.Title;
            model.Kind = survey.Kind;
            model.DefinitionJson = Serialize(survey.Draft);
        }

        return View("Edit", await BuildAsync(model));
    }

    /// <summary>
    /// Saves a new template.
    /// </summary>
    /// <param name="model">The posted form.</param>
    /// <returns>A redirect to the list, or the form with errors.</returns>
    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("surveys/templates/create", "SurveyTemplatesCreate")]
    public async Task<IActionResult> CreatePost(SurveyTemplateEditViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SurveysPermissions.ManageSurveyTemplates))
        {
            return Forbid();
        }

        var template = new SurveyTemplate
        {
            ItemId = IdGenerator.GenerateId(),
            CreatedUtc = _clock.UtcNow,
            OwnerId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            Author = User.Identity?.Name,
        };

        if (!await ApplyAsync(template, model, isNew: true))
        {
            return View("Edit", await BuildAsync(model));
        }

        await _store.CreateAsync(template, HttpContext.RequestAborted);
        await _notifier.SuccessAsync(H["The template was added. New surveys can start from it."]);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Shows the form of a template.
    /// </summary>
    /// <param name="id">The template.</param>
    /// <returns>The page.</returns>
    [Admin("surveys/templates/{id}", "SurveyTemplatesEdit")]
    public async Task<IActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SurveysPermissions.ManageSurveyTemplates))
        {
            return Forbid();
        }

        var template = await _store.FindByIdAsync(id, HttpContext.RequestAborted);

        if (template is null)
        {
            return NotFound();
        }

        return View(await BuildAsync(new SurveyTemplateEditViewModel
        {
            Id = template.ItemId,
            Title = template.Title,
            Name = template.Name,
            Description = template.Description,
            Kind = template.Kind,
            Icon = template.Icon,
            Position = template.Position,
            DefinitionJson = Serialize(template.Definition),
        }));
    }

    /// <summary>
    /// Saves a template.
    /// </summary>
    /// <param name="id">The template.</param>
    /// <param name="model">The posted form.</param>
    /// <returns>A redirect to the list, or the form with errors.</returns>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("surveys/templates/{id}", "SurveyTemplatesEdit")]
    public async Task<IActionResult> EditPost(string id, SurveyTemplateEditViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SurveysPermissions.ManageSurveyTemplates))
        {
            return Forbid();
        }

        var template = await _store.FindByIdAsync(id, HttpContext.RequestAborted);

        if (template is null)
        {
            return NotFound();
        }

        model.Id = template.ItemId;

        if (!await ApplyAsync(template, model, isNew: false))
        {
            return View(await BuildAsync(model));
        }

        template.ModifiedUtc = _clock.UtcNow;
        await _store.UpdateAsync(template, HttpContext.RequestAborted);
        await _notifier.SuccessAsync(H["The template was saved."]);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Removes a template. Surveys made from it keep their questions.
    /// </summary>
    /// <param name="id">The template.</param>
    /// <returns>A redirect to the list.</returns>
    [HttpPost]
    [Admin("surveys/templates/{id}/delete", "SurveyTemplatesDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SurveysPermissions.ManageSurveyTemplates))
        {
            return Forbid();
        }

        var template = await _store.FindByIdAsync(id, HttpContext.RequestAborted);

        if (template is not null)
        {
            await _store.DeleteAsync(template, HttpContext.RequestAborted);
            await _notifier.SuccessAsync(H["The template was removed."]);
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Finds a survey to copy into a template. A template carries the draft with its answer keys, so the user must be
    /// allowed to edit the survey; course-owned surveys are never copied.
    /// </summary>
    private async Task<Survey> FindCopyableSurveyAsync(string surveyId)
    {
        if (string.IsNullOrEmpty(surveyId))
        {
            return null;
        }

        var survey = await _surveys.FindByIdAsync(surveyId, HttpContext.RequestAborted);

        if (survey is null || survey.Owner is not null
            || !await _surveyAuthorization.AuthorizeAsync(User, survey, SurveyOperation.Manage, HttpContext.RequestAborted))
        {
            return null;
        }

        return survey;
    }

    private async Task<bool> ApplyAsync(SurveyTemplate template, SurveyTemplateEditViewModel model, bool isNew)
    {
        var name = SurveyNames.Slugify(string.IsNullOrWhiteSpace(model.Name) ? model.Title : model.Name);

        if (string.IsNullOrWhiteSpace(model.Title))
        {
            ModelState.AddModelError(nameof(model.Title), S["The title is required."]);
        }

        if (string.IsNullOrEmpty(name) || string.Equals(name, SurveyTemplateNames.Blank, StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.Name), S["Enter another technical name."]);
        }
        else if (await _store.FindByNameAsync(name, HttpContext.RequestAborted) is { } existing && existing.ItemId != template.ItemId)
        {
            ModelState.AddModelError(nameof(model.Name), S["Another template already has this technical name."]);
        }

        // The select offers the built-in kinds and a template's own; a posted kind is checked here, since the staff pages
        // show it.
        if (!string.IsNullOrWhiteSpace(model.Kind) && !SurveyKinds.IsValid(model.Kind.Trim()))
        {
            ModelState.AddModelError(nameof(model.Kind), S["Pick a kind from the list."]);
        }

        var definition = new SurveyDefinition();

        if (isNew && string.IsNullOrWhiteSpace(model.DefinitionJson) && !string.IsNullOrEmpty(model.FromSurveyId))
        {
            var survey = await FindCopyableSurveyAsync(model.FromSurveyId);

            if (survey is null)
            {
                ModelState.AddModelError(nameof(model.FromSurveyId), S["Pick a survey you may edit."]);
            }
            else
            {
                definition = survey.Draft ?? definition;
            }
        }
        else if (!string.IsNullOrWhiteSpace(model.DefinitionJson))
        {
            try
            {
                definition = JsonSerializer.Deserialize<SurveyDefinition>(model.DefinitionJson, CatalogDeploymentSerializer.Options) ?? definition;
            }
            catch (JsonException)
            {
                ModelState.AddModelError(nameof(model.DefinitionJson), S["The questions are not valid JSON of a survey definition."]);
            }
        }

        if (!ModelState.IsValid)
        {
            return false;
        }

        SurveyDefinitions.EnsureBasics(definition);

        template.Title = model.Title.Trim();
        template.Name = name;
        template.Description = model.Description?.Trim();
        template.Kind = string.IsNullOrWhiteSpace(model.Kind) ? SurveyKinds.Survey : model.Kind.Trim();
        template.Icon = string.IsNullOrWhiteSpace(model.Icon) ? "fa-solid fa-clipboard-list" : model.Icon.Trim();
        template.Position = model.Position;
        template.Definition = definition;

        return true;
    }

    private async Task<SurveyTemplateEditViewModel> BuildAsync(SurveyTemplateEditViewModel model)
    {
        model.Kinds =
        [
            new SelectListItem(S["Survey"], SurveyKinds.Survey),
            new SelectListItem(S["Quiz"], SurveyKinds.Quiz),
            new SelectListItem(S["Poll"], SurveyKinds.Poll),
        ];

        if (!string.IsNullOrWhiteSpace(model.Kind) && !model.Kinds.Any(kind => kind.Value == model.Kind))
        {
            model.Kinds.Add(new SelectListItem(model.Kind, model.Kind));
        }

        if (model.IsNew)
        {
            model.Surveys = [new SelectListItem(S["No survey: start empty"], string.Empty)];

            // A template copies the draft with its answer keys, so only surveys the user may edit are offered. They are read
            // from the survey index; the survey copied is loaded and checked again when the template is saved.
            var scope = await _scopes.ResolveAsync(User, SurveyOperation.Manage, HttpContext.RequestAborted);
            IReadOnlyList<Survey> surveys = scope.IsEmpty ? [] : await _reader.ListAsync(scope.All ? null : scope.SurveyIds, publishedOnly: false, HttpContext.RequestAborted);

            foreach (var survey in surveys.Where(survey => survey.Owner is null).OrderBy(survey => survey.Title, StringComparer.CurrentCulture))
            {
                model.Surveys.Add(new SelectListItem(survey.Title, survey.ItemId, survey.ItemId == model.FromSurveyId));
            }
        }

        if (!string.IsNullOrWhiteSpace(model.DefinitionJson))
        {
            try
            {
                var definition = JsonSerializer.Deserialize<SurveyDefinition>(model.DefinitionJson, CatalogDeploymentSerializer.Options);
                model.QuestionCount = definition?.Pages?.Sum(page => page.Questions?.Count ?? 0) ?? 0;
            }
            catch (JsonException)
            {
                model.QuestionCount = 0;
            }
        }

        return model;
    }

    private static string Serialize(SurveyDefinition definition)
        => JsonSerializer.Serialize(definition ?? new SurveyDefinition(), _indented);
}
