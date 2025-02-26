using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.OpenAI.Core;
using CrestApps.OrchardCore.OpenAI.Core.Models;
using CrestApps.OrchardCore.OpenAI.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Documents;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Routing;

namespace CrestApps.OrchardCore.OpenAI.Controllers;

[Feature(OpenAIConstants.Feature.Settings)]
public sealed class AdminController : Controller
{
    private const string _optionsSearch = "Options.Search";

    private readonly IAuthorizationService _authorizationService;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly INotifier _notifier;
    private readonly IDocumentManager<OpenAIConnectionDocument> _documentManager;
    private readonly IShellReleaseManager _shellReleaseManager;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public AdminController(
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        INotifier notifier,
        IDocumentManager<OpenAIConnectionDocument> documentManager,
        IShellReleaseManager shellReleaseManager,
        IHtmlLocalizer<AdminController> htmlLocalizer,
        IStringLocalizer<AdminController> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _updateModelAccessor = updateModelAccessor;
        _notifier = notifier;
        _documentManager = documentManager;
        _shellReleaseManager = shellReleaseManager;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    [Admin("openai/connections", "OpenAIConnectionsIndex")]
    public async Task<IActionResult> Index(
        OpenAIConnectionOptions options,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageOpenAIConnections))
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value.GetPageSize());

        var document = await _documentManager.GetOrCreateImmutableAsync();

        var routeData = new RouteData();

        var records = document.Connections.Values.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(options.Search))
        {
            records = records.Where(x => x.Name.Contains(options.Search, StringComparison.OrdinalIgnoreCase) || x.DisplayText.Contains(options.Search, StringComparison.OrdinalIgnoreCase));
        }

        var total = records.Count();

        var model = new ListOpenAIConnectionViewModel
        {
            Count = document.Connections.Values.Count,
            Pager = await shapeFactory.PagerAsync(pager, document.Connections.Values.Count, routeData),
            Records = records.Select(x => new DisplayOpenAIConnectionViewModel
            {
                Id = x.Id,
                Name = x.Name,
                DisplayText = x.DisplayText,
                DefaultDeploymentName = x.DefaultDeploymentName,
                Endpoint = x.Endpoint,
            }).OrderBy(x => x.DisplayText)
            .Skip(pager.GetStartIndex())
            .Take(pager.PageSize)
            .ToArray(),
        };

        return View(model);
    }

    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("openai/connections", "OpenAIConnectionsIndex")]
    public ActionResult IndexFilterPost(ListOpenAIConnectionViewModel model)
    {
        return RedirectToAction(nameof(Index), new RouteValueDictionary
        {
            { _optionsSearch, model.Options?.Search },
        });
    }

    [Admin("openai/connection/create", "OpenAIConnectionCreate")]
    public async Task<ActionResult> Create()
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageOpenAIConnections))
        {
            return Forbid();
        }

        var model = new OpenAIConnectionViewModel();

        return View(model);
    }

    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("openai/connection/create", "OpenAIConnectionCreate")]
    public async Task<ActionResult> Create(OpenAIConnectionViewModel model, [FromServices] IDataProtectionProvider dataProtector)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageOpenAIConnections))
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(model.ApiKey))
        {
            ModelState.AddModelError(nameof(model.ApiKey), S["A API-key is required."]);
        }

        if (ModelState.IsValid)
        {
            var document = await _documentManager.GetOrCreateMutableAsync();

            if (document.Connections.Values.Any(x => string.Equals(x.Name, model.Name, StringComparison.OrdinalIgnoreCase)))
            {
                ModelState.AddModelError(nameof(model.Name), S["A connection with the same name already exists."]);
            }
            else
            {
                var protector = dataProtector.CreateProtector(OpenAIConstants.ConnectionProtectorName);

                var connection = new OpenAIConnection
                {
                    Id = IdGenerator.GenerateId(),
                    Name = model.Name,
                    DisplayText = model.DisplayText,
                    Endpoint = model.Endpoint,
                    ApiKey = protector.Protect(model.ApiKey),
                    DefaultDeploymentName = model.DefaultDeploymentName,
                };

                document.Connections[connection.Id] = connection;

                if (model.IsDefaultConnection)
                {
                    document.DefaultConnectionId = connection.Id;
                }

                await _documentManager.UpdateAsync(document);

                await _notifier.SuccessAsync(H["A connection has been created successfully."]);

                _shellReleaseManager.RequestRelease();

                return RedirectToAction(nameof(Index));
            }
        }

        return View(model);
    }

    [Admin("openai/connection/edit/{id}", "OpenAIConnectionEdit")]
    public async Task<ActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageOpenAIConnections))
        {
            return Forbid();
        }

        var document = await _documentManager.GetOrCreateImmutableAsync();

        if (!document.Connections.TryGetValue(id, out var connection))
        {
            return NotFound();
        }

        var model = new OpenAIConnectionViewModel
        {
            Name = connection.Name,
            DisplayText = connection.DisplayText,
            Endpoint = connection.Endpoint,
            DefaultDeploymentName = connection.DefaultDeploymentName,
            IsDefaultConnection = document.DefaultConnectionId == connection.Id,
        };

        return View(model);
    }

    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("openai/connection/edit/{id}", "OpenAIConnectionEdit")]
    public async Task<ActionResult> Edit(string id, OpenAIConnectionViewModel model, [FromServices] IDataProtectionProvider dataProtector)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageOpenAIConnections))
        {
            return Forbid();
        }

        var document = await _documentManager.GetOrCreateMutableAsync();

        if (!document.Connections.TryGetValue(id, out var connection))
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            var protector = dataProtector.CreateProtector(OpenAIConstants.ConnectionProtectorName);

            connection.Name = model.Name;
            connection.DisplayText = model.DisplayText;
            connection.Endpoint = model.Endpoint;
            connection.DefaultDeploymentName = model.DefaultDeploymentName;

            if (!string.IsNullOrWhiteSpace(model.ApiKey))
            {
                connection.ApiKey = protector.Protect(model.ApiKey);
            }

            document.Connections[connection.Id] = connection;

            if (model.IsDefaultConnection)
            {
                document.DefaultConnectionId = connection.Id;
            }

            await _documentManager.UpdateAsync(document);

            await _notifier.SuccessAsync(H["A connection has been updated successfully."]);

            _shellReleaseManager.RequestRelease();

            return RedirectToAction(nameof(Index));
        }

        return View(model);
    }

    [HttpPost]
    [Admin("openai/connection/delete/{id}", "OpenAIConnectionDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageOpenAIConnections))
        {
            return Forbid();
        }

        var document = await _documentManager.GetOrCreateMutableAsync();

        if (!document.Connections.TryGetValue(id, out var connection))
        {
            return NotFound();
        }

        document.Connections.Remove(id);

        if (document.DefaultConnectionId == id)
        {
            document.DefaultConnectionId = null;
        }

        await _documentManager.UpdateAsync(document);

        await _notifier.SuccessAsync(H["A connection has been deleted successfully."]);

        _shellReleaseManager.RequestRelease();

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.BulkAction")]
    [Admin("openai/connections", "OpenAIConnectionsIndex")]

    public async Task<ActionResult> IndexPost(OpenAIConnectionOptions options, IEnumerable<string> itemIds)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageOpenAIConnections))
        {
            return Forbid();
        }

        if (itemIds?.Count() > 0)
        {
            switch (options.BulkAction)
            {
                case OpenAIConnectionAction.None:
                    break;
                case OpenAIConnectionAction.Remove:
                    var counter = 0;
                    var document = await _documentManager.GetOrCreateMutableAsync();
                    foreach (var id in itemIds)
                    {
                        if (document.Connections.Remove(id))
                        {
                            counter++;
                        }
                    }
                    if (counter == 0)
                    {
                        await _notifier.WarningAsync(H["No connections were removed."]);
                    }
                    else
                    {
                        _shellReleaseManager.RequestRelease();

                        await _notifier.SuccessAsync(H.Plural(counter, "1 connection has been removed successfully.", "{0} connections have been removed successfully."));
                    }
                    break;
                default:
                    return BadRequest();
            }
        }

        return RedirectToAction(nameof(Index));
    }
}
