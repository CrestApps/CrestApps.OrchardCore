using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Controllers;

/// <summary>
/// Provides endpoints for managing subject flows.
/// </summary>
[Admin]
public sealed class SubjectFlowsController : Controller
{
    private readonly ICatalog<SubjectFlowSettings> _flowSettingsCatalog;
    private readonly ICatalogManager<SubjectFlowSettings> _flowSettingsManager;
    private readonly ISourceCatalogManager<SubjectAction> _actionManager;
    private readonly ISourceCatalog<SubjectAction> _actionCatalog;
    private readonly INamedCatalog<OmnichannelDisposition> _dispositionsCatalog;
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IDisplayManager<SubjectFlowSettings> _flowDisplayDriver;
    private readonly IDisplayManager<SubjectAction> _actionDisplayDriver;
    private readonly SubjectActionOptions _actionOptions;
    private readonly INotifier _notifier;
    private readonly ISubjectFlowSettingsService _subjectFlowSettingsService;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubjectFlowsController"/> class.
    /// </summary>
    /// <param name="flowSettingsCatalog">The flow settings catalog.</param>
    /// <param name="flowSettingsManager">The flow settings manager.</param>
    /// <param name="actionManager">The subject action manager.</param>
    /// <param name="actionCatalog">The subject action catalog.</param>
    /// <param name="dispositionsCatalog">The dispositions catalog.</param>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="updateModelAccessor">The update model accessor.</param>
    /// <param name="flowDisplayDriver">The flow settings display driver.</param>
    /// <param name="actionDisplayDriver">The subject action display driver.</param>
    /// <param name="actionOptions">The subject action options.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="subjectFlowSettingsService">The subject flow settings service.</param>
    /// <param name="htmlLocalizer">The html localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SubjectFlowsController(
        ICatalog<SubjectFlowSettings> flowSettingsCatalog,
        ICatalogManager<SubjectFlowSettings> flowSettingsManager,
        ISourceCatalogManager<SubjectAction> actionManager,
        ISourceCatalog<SubjectAction> actionCatalog,
        INamedCatalog<OmnichannelDisposition> dispositionsCatalog,
        IContentDefinitionManager contentDefinitionManager,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<SubjectFlowSettings> flowDisplayDriver,
        IDisplayManager<SubjectAction> actionDisplayDriver,
        IOptions<SubjectActionOptions> actionOptions,
        INotifier notifier,
        ISubjectFlowSettingsService subjectFlowSettingsService,
        IHtmlLocalizer<SubjectFlowsController> htmlLocalizer,
        IStringLocalizer<SubjectFlowsController> stringLocalizer)
    {
        _flowSettingsCatalog = flowSettingsCatalog;
        _flowSettingsManager = flowSettingsManager;
        _actionManager = actionManager;
        _actionCatalog = actionCatalog;
        _dispositionsCatalog = dispositionsCatalog;
        _contentDefinitionManager = contentDefinitionManager;
        _authorizationService = authorizationService;
        _updateModelAccessor = updateModelAccessor;
        _flowDisplayDriver = flowDisplayDriver;
        _actionDisplayDriver = actionDisplayDriver;
        _actionOptions = actionOptions.Value;
        _notifier = notifier;
        _subjectFlowSettingsService = subjectFlowSettingsService;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists all subject content types and their flow configuration status.
    /// </summary>
    [Admin("omnichannel/subject-flows", "OmnichannelSubjectFlows")]
    public async Task<ActionResult> Index()
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ManageSubjectFlows))
        {
            return Forbid();
        }

        var contentTypes = await _contentDefinitionManager.ListTypeDefinitionsAsync();

        var subjectTypes = contentTypes
            .Where(t => t.StereotypeEquals(OmnichannelConstants.Sterotypes.OmnichannelSubject))
            .OrderBy(t => t.DisplayName)
            .ToList();

        var allFlowSettings = await _flowSettingsCatalog.GetAllAsync();
        var flowSettingsMap = allFlowSettings.ToDictionary(
            f => f.SubjectContentType,
            f => f,
            StringComparer.OrdinalIgnoreCase);

        var allActions = await _actionCatalog.GetAllAsync();
        var actionsPerSubject = allActions
            .GroupBy(a => a.SubjectContentType, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        var model = new SubjectFlowsIndexViewModel
        {
            Subjects = subjectTypes.Select(t => new SubjectFlowEntryViewModel
            {
                ContentTypeName = t.Name,
                DisplayName = t.DisplayName,
                IsConfigured = flowSettingsMap.TryGetValue(t.Name, out var flowSettings) && _subjectFlowSettingsService.IsConfigured(flowSettings),
                HasActions = actionsPerSubject.TryGetValue(t.Name, out var count) && count > 0,
            }).ToList(),
        };

        return View(model);
    }

    /// <summary>
    /// Configures the flow for a specific subject content type.
    /// </summary>
    /// <param name="subjectContentType">The subject content type name.</param>
    [Admin("omnichannel/subject-flows/{subjectContentType}/configure", "OmnichannelSubjectFlowsConfigure")]
    public async Task<ActionResult> Configure(string subjectContentType)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ManageSubjectFlows))
        {
            return Forbid();
        }

        var contentType = await _contentDefinitionManager.GetTypeDefinitionAsync(subjectContentType);

        if (contentType is null || !contentType.StereotypeEquals(OmnichannelConstants.Sterotypes.OmnichannelSubject))
        {
            return NotFound();
        }

        var allFlowSettings = await _flowSettingsCatalog.GetAllAsync();
        var flowSettings = allFlowSettings.FirstOrDefault(f =>
            string.Equals(f.SubjectContentType, subjectContentType, StringComparison.OrdinalIgnoreCase));

        var isNew = flowSettings is null;

        if (isNew)
        {
            flowSettings = await _flowSettingsManager.NewAsync();
            flowSettings.SubjectContentType = subjectContentType;
        }

        var model = new SubjectFlowConfigureViewModel
        {
            SubjectContentType = subjectContentType,
            SubjectDisplayName = contentType.DisplayName,
            Editor = await _flowDisplayDriver.BuildEditorAsync(flowSettings, _updateModelAccessor.ModelUpdater, isNew: isNew),
        };

        return View(model);
    }

    /// <summary>
    /// Saves the flow configuration for a specific subject content type.
    /// </summary>
    /// <param name="subjectContentType">The subject content type name.</param>
    [HttpPost]
    [ActionName(nameof(Configure))]
    [Admin("omnichannel/subject-flows/{subjectContentType}/configure", "OmnichannelSubjectFlowsConfigure")]
    public async Task<ActionResult> ConfigurePost(string subjectContentType)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ManageSubjectFlows))
        {
            return Forbid();
        }

        var contentType = await _contentDefinitionManager.GetTypeDefinitionAsync(subjectContentType);

        if (contentType is null || !contentType.StereotypeEquals(OmnichannelConstants.Sterotypes.OmnichannelSubject))
        {
            return NotFound();
        }

        var allFlowSettings = await _flowSettingsCatalog.GetAllAsync();
        var flowSettings = allFlowSettings.FirstOrDefault(f =>
            string.Equals(f.SubjectContentType, subjectContentType, StringComparison.OrdinalIgnoreCase));

        var isNew = flowSettings is null;

        if (isNew)
        {
            flowSettings = await _flowSettingsManager.NewAsync();
            flowSettings.SubjectContentType = subjectContentType;
        }

        await _flowDisplayDriver.UpdateEditorAsync(flowSettings, _updateModelAccessor.ModelUpdater, isNew: isNew);

        if (ModelState.IsValid)
        {
            if (isNew)
            {
                await _flowSettingsManager.CreateAsync(flowSettings);
            }
            else
            {
                await _flowSettingsManager.UpdateAsync(flowSettings);
            }

            await _notifier.SuccessAsync(H["The subject flow settings have been saved successfully."]);

            return RedirectToAction(nameof(Configure), new { subjectContentType });
        }

        var model = new SubjectFlowConfigureViewModel
        {
            SubjectContentType = subjectContentType,
            SubjectDisplayName = contentType.DisplayName,
            Editor = await _flowDisplayDriver.BuildEditorAsync(flowSettings, _updateModelAccessor.ModelUpdater, isNew: isNew),
        };

        return View(model);
    }

    /// <summary>
    /// Manages the subject actions for a specific subject content type.
    /// </summary>
    /// <param name="subjectContentType">The subject content type name.</param>
    [Admin("omnichannel/subject-flows/{subjectContentType}/actions", "OmnichannelSubjectFlowsManageActions")]
    public async Task<ActionResult> ManageActions(string subjectContentType)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ManageSubjectFlows))
        {
            return Forbid();
        }

        var contentType = await _contentDefinitionManager.GetTypeDefinitionAsync(subjectContentType);

        if (contentType is null || !contentType.StereotypeEquals(OmnichannelConstants.Sterotypes.OmnichannelSubject))
        {
            return NotFound();
        }

        var allActions = await _actionCatalog.GetAllAsync();
        var subjectActions = allActions
            .Where(a => string.Equals(a.SubjectContentType, subjectContentType, StringComparison.OrdinalIgnoreCase))
            .OrderBy(a => a.DispositionId)
            .ThenBy(a => a.Source)
            .ToList();

        var dispositions = await _dispositionsCatalog.GetAllAsync();
        var dispositionMap = dispositions.ToDictionary(d => d.ItemId, d => d.Name, StringComparer.OrdinalIgnoreCase);

        var actionEntries = new List<SubjectActionEntryViewModel>();

        foreach (var action in subjectActions)
        {
            dispositionMap.TryGetValue(action.DispositionId ?? string.Empty, out var dispositionText);

            var typeDisplayName = _actionOptions.ActionTypes.TryGetValue(action.Source, out var typeEntry)
                ? typeEntry.DisplayName?.Value
                : action.Source;

            actionEntries.Add(new SubjectActionEntryViewModel
            {
                Model = action,
                DispositionDisplayText = dispositionText ?? action.DispositionId,
                ActionTypeDisplayName = typeDisplayName ?? action.Source,
            });
        }

        var model = new ManageSubjectActionsViewModel
        {
            SubjectContentType = subjectContentType,
            SubjectDisplayName = contentType.DisplayName,
            Actions = actionEntries,
            ActionTypes = _actionOptions.ActionTypes.Values,
        };

        return View(model);
    }

    /// <summary>
    /// Creates a new subject action for the given subject content type.
    /// </summary>
    /// <param name="subjectContentType">The subject content type.</param>
    /// <param name="source">The action type source.</param>
    [Admin("omnichannel/subject-flows/{subjectContentType}/actions/create/{source}", "OmnichannelSubjectActionsCreate")]
    public async Task<ActionResult> CreateAction(string subjectContentType, string source)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ManageSubjectFlows))
        {
            return Forbid();
        }

        var contentType = await _contentDefinitionManager.GetTypeDefinitionAsync(subjectContentType);

        if (contentType is null || !contentType.StereotypeEquals(OmnichannelConstants.Sterotypes.OmnichannelSubject))
        {
            return NotFound();
        }

        if (!_actionOptions.ActionTypes.TryGetValue(source, out var entry))
        {
            await _notifier.ErrorAsync(H["Unable to find an action type with the name '{0}'.", source]);

            return RedirectToAction(nameof(ManageActions), new { subjectContentType });
        }

        var model = await _actionManager.NewAsync(entry.Type);
        model.SubjectContentType = subjectContentType;

        var viewModel = new EditSubjectActionViewModel
        {
            SubjectContentType = subjectContentType,
            SubjectDisplayName = contentType.DisplayName,
            ActionTypeDisplayName = entry.DisplayName?.Value ?? entry.Type,
            Editor = await _actionDisplayDriver.BuildEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: true),
        };

        return View(viewModel);
    }

    /// <summary>
    /// Creates a new subject action for the given subject content type.
    /// </summary>
    /// <param name="subjectContentType">The subject content type.</param>
    /// <param name="source">The action type source.</param>
    [HttpPost]
    [ActionName(nameof(CreateAction))]
    [Admin("omnichannel/subject-flows/{subjectContentType}/actions/create/{source}", "OmnichannelSubjectActionsCreate")]
    public async Task<ActionResult> CreateActionPost(string subjectContentType, string source)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ManageSubjectFlows))
        {
            return Forbid();
        }

        var contentType = await _contentDefinitionManager.GetTypeDefinitionAsync(subjectContentType);

        if (contentType is null || !contentType.StereotypeEquals(OmnichannelConstants.Sterotypes.OmnichannelSubject))
        {
            return NotFound();
        }

        if (!_actionOptions.ActionTypes.TryGetValue(source, out var entry))
        {
            await _notifier.ErrorAsync(H["Unable to find an action type with the name '{0}'.", source]);

            return RedirectToAction(nameof(ManageActions), new { subjectContentType });
        }

        var model = await _actionManager.NewAsync(entry.Type);
        model.SubjectContentType = subjectContentType;

        var viewModel = new EditSubjectActionViewModel
        {
            SubjectContentType = subjectContentType,
            SubjectDisplayName = contentType.DisplayName,
            ActionTypeDisplayName = entry.DisplayName?.Value ?? entry.Type,
            Editor = await _actionDisplayDriver.UpdateEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: true),
        };

        if (ModelState.IsValid)
        {
            await _actionManager.CreateAsync(model);
            await _notifier.SuccessAsync(H["A new subject action has been created successfully."]);

            return RedirectToAction(nameof(ManageActions), new { subjectContentType });
        }

        return View(viewModel);
    }

    /// <summary>
    /// Edits an existing subject action.
    /// </summary>
    /// <param name="id">The subject action identifier.</param>
    [Admin("omnichannel/subject-actions/edit/{id}", "OmnichannelSubjectActionsEdit")]
    public async Task<ActionResult> EditAction(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ManageSubjectFlows))
        {
            return Forbid();
        }

        var model = await _actionManager.FindByIdAsync(id);

        if (model is null)
        {
            return NotFound();
        }

        var contentType = await _contentDefinitionManager.GetTypeDefinitionAsync(model.SubjectContentType);

        var viewModel = new EditSubjectActionViewModel
        {
            SubjectContentType = model.SubjectContentType,
            SubjectDisplayName = contentType?.DisplayName ?? model.SubjectContentType,
            ActionTypeDisplayName = _actionOptions.ActionTypes.TryGetValue(model.Source, out var entry)
                ? entry.DisplayName?.Value ?? model.Source
                : model.Source,
            Editor = await _actionDisplayDriver.BuildEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: false),
        };

        return View(viewModel);
    }

    /// <summary>
    /// Edits an existing subject action.
    /// </summary>
    /// <param name="id">The subject action identifier.</param>
    [HttpPost]
    [ActionName(nameof(EditAction))]
    [Admin("omnichannel/subject-actions/edit/{id}", "OmnichannelSubjectActionsEdit")]
    public async Task<ActionResult> EditActionPost(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ManageSubjectFlows))
        {
            return Forbid();
        }

        var model = await _actionManager.FindByIdAsync(id);

        if (model is null)
        {
            return NotFound();
        }

        var contentType = await _contentDefinitionManager.GetTypeDefinitionAsync(model.SubjectContentType);

        var viewModel = new EditSubjectActionViewModel
        {
            SubjectContentType = model.SubjectContentType,
            SubjectDisplayName = contentType?.DisplayName ?? model.SubjectContentType,
            ActionTypeDisplayName = _actionOptions.ActionTypes.TryGetValue(model.Source, out var entry)
                ? entry.DisplayName?.Value ?? model.Source
                : model.Source,
            Editor = await _actionDisplayDriver.UpdateEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: false),
        };

        if (ModelState.IsValid)
        {
            await _actionManager.UpdateAsync(model);
            await _notifier.SuccessAsync(H["The subject action has been updated successfully."]);

            return RedirectToAction(nameof(ManageActions), new { subjectContentType = model.SubjectContentType });
        }

        return View(viewModel);
    }

    /// <summary>
    /// Deletes a subject action.
    /// </summary>
    /// <param name="id">The subject action identifier.</param>
    [HttpPost]
    [Admin("omnichannel/subject-actions/delete/{id}", "OmnichannelSubjectActionsDelete")]
    public async Task<IActionResult> DeleteAction(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ManageSubjectFlows))
        {
            return Forbid();
        }

        var model = await _actionManager.FindByIdAsync(id);

        if (model is null)
        {
            return NotFound();
        }

        var subjectContentType = model.SubjectContentType;

        if (await _actionManager.DeleteAsync(model))
        {
            await _notifier.SuccessAsync(H["The subject action has been deleted successfully."]);
        }
        else
        {
            await _notifier.ErrorAsync(H["Unable to remove the subject action."]);
        }

        return RedirectToAction(nameof(ManageActions), new { subjectContentType });
    }
}
