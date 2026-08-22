using CrestApps.OrchardCore.Core.Models;
using CrestApps.OrchardCore.Core.Validation;
using CrestApps.OrchardCore.Telephony.Sms.Core;
using CrestApps.OrchardCore.Telephony.Sms.Core.Models;
using CrestApps.OrchardCore.Telephony.Sms.Core.Services;
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
using OrchardCore.Navigation;
using OrchardCore.Routing;
using QueryContext = CrestApps.Core.Models.QueryContext;

namespace CrestApps.OrchardCore.Telephony.Sms.Controllers;

/// <summary>
/// Administers the SMS canned-response templates.
/// </summary>
[Admin]
public sealed class SmsTemplatesController : Controller
{
    private const string _optionsSearch = "Options.Search";

    private readonly ISmsTemplateManager _manager;
    private readonly IAuthorizationService _authorizationService;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IDisplayManager<SmsTemplate> _displayManager;
    private readonly INotifier _notifier;

    private readonly IHtmlLocalizer H;
    private readonly IStringLocalizer S;

    public SmsTemplatesController(
        ISmsTemplateManager manager,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<SmsTemplate> displayManager,
        INotifier notifier,
        IHtmlLocalizer<SmsTemplatesController> htmlLocalizer,
        IStringLocalizer<SmsTemplatesController> stringLocalizer)
    {
        _manager = manager;
        _authorizationService = authorizationService;
        _updateModelAccessor = updateModelAccessor;
        _displayManager = displayManager;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    [Admin("sms/templates", "SmsTemplatesIndex")]
    public async Task<IActionResult> Index(
        CatalogEntryOptions options,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TelephonySmsPermissions.ManageSmsNumberRoutes))
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value.GetPageSize());
        var result = await _manager.PageAsync(pager.Page, pager.PageSize, new QueryContext { Sorted = true, Name = options.Search });

        var routeData = new RouteData();

        if (!string.IsNullOrEmpty(options.Search))
        {
            routeData.Values.TryAdd(_optionsSearch, options.Search);
        }

        var viewModel = new ListCatalogEntryViewModel<CatalogEntryViewModel<SmsTemplate>>
        {
            Models = [],
            Options = options,
            Pager = await shapeFactory.PagerAsync(pager, result.Count, routeData),
        };

        foreach (var model in result.Entries)
        {
            viewModel.Models.Add(new CatalogEntryViewModel<SmsTemplate>
            {
                Model = model,
                Shape = await _displayManager.BuildDisplayAsync(model, _updateModelAccessor.ModelUpdater, "SummaryAdmin"),
            });
        }

        viewModel.Options.BulkActions =
        [
            new SelectListItem(S["Delete"], nameof(CatalogEntryAction.Remove)),
        ];

        return View(viewModel);
    }

    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("sms/templates", "SmsTemplatesIndex")]
    public async Task<IActionResult> IndexFilterPost(ListCatalogEntryViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TelephonySmsPermissions.ManageSmsNumberRoutes))
        {
            return Forbid();
        }

        return RedirectToAction(nameof(Index), new RouteValueDictionary { { _optionsSearch, model.Options?.Search } });
    }

    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.BulkAction")]
    [Admin("sms/templates", "SmsTemplatesIndex")]
    public async Task<IActionResult> IndexBulkPost(CatalogEntryOptions options, IEnumerable<string> itemIds)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TelephonySmsPermissions.ManageSmsNumberRoutes))
        {
            return Forbid();
        }

        if (itemIds?.Any() == true && options.BulkAction == CatalogEntryAction.Remove)
        {
            var counter = 0;

            foreach (var id in itemIds)
            {
                var template = await _manager.FindByIdAsync(id);

                if (template is not null && await _manager.DeleteAsync(template))
                {
                    counter++;
                }
            }

            if (counter == 0)
            {
                await _notifier.WarningAsync(H["No templates were removed."]);
            }
            else
            {
                await _notifier.SuccessAsync(H.Plural(counter, "1 template has been removed successfully.", "{0} templates have been removed successfully."));
            }
        }

        return RedirectToAction(nameof(Index));
    }

    [Admin("sms/templates/create", "SmsTemplatesCreate")]
    public async Task<IActionResult> Create()
    {
        if (!await _authorizationService.AuthorizeAsync(User, TelephonySmsPermissions.ManageSmsNumberRoutes))
        {
            return Forbid();
        }

        var model = await _manager.NewAsync();

        return View(new EditCatalogEntryViewModel
        {
            DisplayName = S["SMS template"],
            Editor = await _displayManager.BuildEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: true),
        });
    }

    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("sms/templates/create", "SmsTemplatesCreate")]
    public async Task<IActionResult> CreatePost()
    {
        if (!await _authorizationService.AuthorizeAsync(User, TelephonySmsPermissions.ManageSmsNumberRoutes))
        {
            return Forbid();
        }

        var model = await _manager.NewAsync();
        var viewModel = new EditCatalogEntryViewModel
        {
            DisplayName = S["New SMS template"],
            Editor = await _displayManager.UpdateEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: true),
        };

        var isValid = await CatalogEntryValidation.ValidateAsync(_manager, model, _updateModelAccessor.ModelUpdater, nameof(SmsTemplate));

        if (isValid && ModelState.IsValid)
        {
            await _manager.CreateAsync(model);
            await _notifier.SuccessAsync(H["The SMS template has been created successfully."]);

            return RedirectToAction(nameof(Index));
        }

        return View(viewModel);
    }

    [Admin("sms/templates/edit/{id}", "SmsTemplatesEdit")]
    public async Task<IActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TelephonySmsPermissions.ManageSmsNumberRoutes))
        {
            return Forbid();
        }

        var model = await _manager.FindByIdAsync(id);

        if (model is null)
        {
            return NotFound();
        }

        return View(new EditCatalogEntryViewModel
        {
            DisplayName = model.Name,
            Editor = await _displayManager.BuildEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: false),
        });
    }

    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("sms/templates/edit/{id}", "SmsTemplatesEdit")]
    public async Task<IActionResult> EditPost(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TelephonySmsPermissions.ManageSmsNumberRoutes))
        {
            return Forbid();
        }

        var model = await _manager.FindByIdAsync(id);

        if (model is null)
        {
            return NotFound();
        }

        var viewModel = new EditCatalogEntryViewModel
        {
            DisplayName = model.Name,
            Editor = await _displayManager.UpdateEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: false),
        };

        var isValid = await CatalogEntryValidation.ValidateAsync(_manager, model, _updateModelAccessor.ModelUpdater, nameof(SmsTemplate));

        if (isValid && ModelState.IsValid)
        {
            await _manager.UpdateAsync(model);
            await _notifier.SuccessAsync(H["The SMS template has been updated successfully."]);

            return RedirectToAction(nameof(Index));
        }

        return View(viewModel);
    }

    [HttpPost]
    [Admin("sms/templates/delete/{id}", "SmsTemplatesDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TelephonySmsPermissions.ManageSmsNumberRoutes))
        {
            return Forbid();
        }

        var model = await _manager.FindByIdAsync(id);

        if (model is not null)
        {
            await _manager.DeleteAsync(model);
            await _notifier.SuccessAsync(H["The SMS template has been deleted successfully."]);
        }

        return RedirectToAction(nameof(Index));
    }
}
