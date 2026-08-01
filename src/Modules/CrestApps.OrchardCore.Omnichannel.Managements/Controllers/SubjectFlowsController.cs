using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Validation;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Controllers;

/// <summary>
/// Provides endpoints for managing subject flows.
/// </summary>
[Admin]
public sealed class SubjectFlowsController : Controller
{
    private readonly ISourceCatalogManager<SubjectAction> _actionManager;
    private readonly ISourceCatalog<SubjectAction> _actionCatalog;
    private readonly INamedCatalog<OmnichannelDisposition> _dispositionsCatalog;
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IDisplayManager<SubjectAction> _actionDisplayDriver;
    private readonly SubjectActionOptions _actionOptions;
    private readonly INotifier _notifier;
    private readonly ISubjectFlowSettingsService _subjectFlowSettingsService;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubjectFlowsController"/> class.
    /// </summary>
    /// <param name="actionManager">The subject action manager.</param>
    /// <param name="actionCatalog">The subject action catalog.</param>
    /// <param name="dispositionsCatalog">The dispositions catalog.</param>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="updateModelAccessor">The update model accessor.</param>
    /// <param name="actionDisplayDriver">The subject action display driver.</param>
    /// <param name="actionOptions">The subject action options.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="subjectFlowSettingsService">The subject flow settings service.</param>
    /// <param name="htmlLocalizer">The html localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SubjectFlowsController(
        ISourceCatalogManager<SubjectAction> actionManager,
        ISourceCatalog<SubjectAction> actionCatalog,
        INamedCatalog<OmnichannelDisposition> dispositionsCatalog,
        IContentDefinitionManager contentDefinitionManager,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<SubjectAction> actionDisplayDriver,
        IOptions<SubjectActionOptions> actionOptions,
        INotifier notifier,
        ISubjectFlowSettingsService subjectFlowSettingsService,
        IHtmlLocalizer<SubjectFlowsController> htmlLocalizer,
        IStringLocalizer<SubjectFlowsController> stringLocalizer)
    {
        _actionManager = actionManager;
        _actionCatalog = actionCatalog;
        _dispositionsCatalog = dispositionsCatalog;
        _contentDefinitionManager = contentDefinitionManager;
        _authorizationService = authorizationService;
        _updateModelAccessor = updateModelAccessor;
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

        var subjectTypes = await _subjectFlowSettingsService.GetConfiguredSubjectTypesAsync();

        var allActions = await _actionCatalog.GetAllAsync();
        var actionsPerSubject = allActions
            .GroupBy(a => a.SubjectContentType, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        var canEditContentTypes = await _authorizationService.AuthorizeAsync(User, new Permission("EditContentTypes"));

        var entries = new List<SubjectFlowEntryViewModel>();

        foreach (var subjectType in subjectTypes.OrderBy(t => t.DisplayName))
        {
            var flowSettings = await _subjectFlowSettingsService.FindConfiguredFlowSettingsAsync(subjectType.Name);

            entries.Add(new SubjectFlowEntryViewModel
            {
                ContentTypeName = subjectType.Name,
                DisplayName = subjectType.DisplayName,
                Direction = flowSettings?.Direction ?? SubjectDirection.Outbound,
                InteractionType = flowSettings?.InteractionType ?? ActivityInteractionType.Manual,
                Channel = flowSettings?.Channel,
                HasActions = actionsPerSubject.TryGetValue(subjectType.Name, out var count) && count > 0,
            });
        }

        var model = new SubjectFlowsIndexViewModel
        {
            Subjects = entries,
            CanEditContentTypes = canEditContentTypes,
        };

        return View(model);
    }
    /// <param name="subjectContentType">The subject content type name.</param>
    [Admin("omnichannel/subject-flows/{subjectContentType}/actions", "OmnichannelSubjectFlowsManageActions")]
    public async Task<ActionResult> ManageActions(string subjectContentType)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ManageSubjectFlows))
        {
            return Forbid();
        }

        var contentType = await _contentDefinitionManager.GetTypeDefinitionAsync(subjectContentType);

        if (!OmnichannelSubjectDefinitionService.HasOmnichannelSubjectPart(contentType))
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

        if (!OmnichannelSubjectDefinitionService.HasOmnichannelSubjectPart(contentType))
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

        if (!OmnichannelSubjectDefinitionService.HasOmnichannelSubjectPart(contentType))
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

        var isValid = await CatalogEntryValidation.ValidateAsync(_actionManager, model, _updateModelAccessor.ModelUpdater, nameof(SubjectAction));

        if (isValid && ModelState.IsValid)
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

        var isValid = await CatalogEntryValidation.ValidateAsync(_actionManager, model, _updateModelAccessor.ModelUpdater, nameof(SubjectAction));

        if (isValid && ModelState.IsValid)
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
