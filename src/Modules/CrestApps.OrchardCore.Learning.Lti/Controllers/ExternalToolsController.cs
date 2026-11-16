using CrestApps.OrchardCore.Core.Controllers;
using CrestApps.OrchardCore.Core.Models;
using CrestApps.OrchardCore.Learning.Lti.Core.Services;
using CrestApps.OrchardCore.Learning.Lti.Models;
using CrestApps.OrchardCore.Learning.Lti.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Navigation;
using OrchardCore.Routing;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Learning.Lti.Controllers;

/// <summary>
/// Learning › External Tools: the registered LTI 1.3 tools, each saved with its OpenID application.
/// </summary>
[Admin]
public sealed class ExternalToolsController : CatalogAdminController<ExternalTool>
{
    private readonly ExternalToolClients _clients;
    private readonly ILogger _logger;

    public ExternalToolsController(
        IExternalToolManager manager,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<ExternalTool> displayManager,
        INotifier notifier,
        ExternalToolClients clients,
        ILogger<ExternalToolsController> logger,
        IHtmlLocalizer<ExternalToolsController> htmlLocalizer,
        IStringLocalizer<ExternalToolsController> stringLocalizer)
        : base(manager, authorizationService, updateModelAccessor, displayManager, notifier, htmlLocalizer, stringLocalizer)
    {
        _clients = clients;
        _logger = logger;
    }

    protected override Permission ManagePermission
        => LearningLtiPermissions.ManageExternalTools;

    protected override LocalizedString CreateDisplayName
        => S["External tool"];

    protected override LocalizedString NewDisplayName
        => S["New external tool"];

    protected override LocalizedHtmlString CreatedNotification
        => H["The tool has been registered. Give it the platform details shown on its page."];

    protected override LocalizedHtmlString UpdatedNotification
        => H["The tool has been updated."];

    protected override LocalizedHtmlString DeletedNotification
        => H["The tool has been removed. Its course items no longer launch."];

    [Admin("learning/external-tools", "LearningExternalToolsIndex")]
    public Task<IActionResult> Index(
        CatalogEntryOptions options,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
        => IndexAsync(options, pagerParameters, pagerOptions, shapeFactory);

    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("learning/external-tools", "LearningExternalToolsIndex")]
    public Task<IActionResult> IndexFilterPost(ListCatalogEntryViewModel model, PagerParameters pagerParameters)
        => IndexFilterPostAsync(model, pagerParameters);

    [Admin("learning/external-tools/create", "LearningExternalToolsCreate")]
    public Task<IActionResult> Create()
        => CreateAsync();

    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("learning/external-tools/create", "LearningExternalToolsCreate")]
    public Task<IActionResult> CreatePost()
        => CreatePostAsync();

    [Admin("learning/external-tools/edit/{id}", "LearningExternalToolsEdit")]
    public Task<IActionResult> Edit(string id)
        => EditAsync(id);

    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("learning/external-tools/edit/{id}", "LearningExternalToolsEdit")]
    public Task<IActionResult> EditPost(string id)
        => EditPostAsync(id);

    [HttpPost]
    [Admin("learning/external-tools/delete/{id}", "LearningExternalToolsDelete")]
    public Task<IActionResult> Delete(string id)
        => DeleteAsync(id);

    protected override async Task OnSavedAsync(ExternalTool model, bool isNew)
    {
        try
        {
            await _clients.SyncAsync(model);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "The OpenID application of an external tool could not be saved.");
            await Notifier.ErrorAsync(H["The tool was saved, but its OpenID application could not be: {0}", exception.Message]);
        }
    }
}
