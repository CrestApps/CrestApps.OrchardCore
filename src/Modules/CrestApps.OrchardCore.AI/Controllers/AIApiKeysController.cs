using CrestApps.Core;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.ApiKeys;
using CrestApps.OrchardCore.AI.ViewModels;
using CrestApps.OrchardCore.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Routing;
using QueryContext = CrestApps.Core.Models.QueryContext;

namespace CrestApps.OrchardCore.AI.Controllers;

/// <summary>
/// API keys: server keys that call the AI API with the permissions of their roles, and browser keys that chat with
/// chosen profiles from chosen web sites.
/// </summary>
[Admin]
[Feature(AIConstants.Feature.ChatApi)]
public sealed class AIApiKeysController : Controller
{
    private const string _optionsSearch = "Options.Search";

    private readonly IAIApiKeyStore _store;
    private readonly IAuthorizationService _authorizationService;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IDisplayManager<AIApiKey> _displayManager;
    private readonly INotifier _notifier;
    private readonly IClock _clock;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIApiKeysController"/> class.
    /// </summary>
    /// <param name="store">The key store.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="updateModelAccessor">The update model accessor.</param>
    /// <param name="displayManager">The key display manager.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIApiKeysController(
        IAIApiKeyStore store,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<AIApiKey> displayManager,
        INotifier notifier,
        IClock clock,
        IHtmlLocalizer<AIApiKeysController> htmlLocalizer,
        IStringLocalizer<AIApiKeysController> stringLocalizer)
    {
        _store = store;
        _authorizationService = authorizationService;
        _updateModelAccessor = updateModelAccessor;
        _displayManager = displayManager;
        _notifier = notifier;
        _clock = clock;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists the keys.
    /// </summary>
    /// <param name="options">The search.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The list.</returns>
    [Admin("ai/api-keys", "AIApiKeysIndex")]
    public async Task<IActionResult> Index(
        CatalogEntryOptions options,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIApiKeys))
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var result = await _store.PageAsync(pager.Page, pager.PageSize, new QueryContext { Name = options.Search });
        var routeData = new RouteData();

        if (!string.IsNullOrEmpty(options.Search))
        {
            routeData.Values.TryAdd(_optionsSearch, options.Search);
        }

        return View(new ListCatalogEntryViewModel
        {
            Options = options,
            List = new AdminListContext<AIApiKey>
            {
                Source = result.Entries,
                Pager = await shapeFactory.PagerAsync(pager, result.Count, routeData),
            },
        });
    }

    /// <summary>
    /// Applies the search.
    /// </summary>
    /// <param name="model">The submitted list.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <returns>A redirect to the filtered list.</returns>
    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("ai/api-keys", "AIApiKeysIndex")]
    public async Task<IActionResult> IndexFilterPost(ListCatalogEntryViewModel model, PagerParameters pagerParameters)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIApiKeys))
        {
            return Forbid();
        }

        return RedirectToAction(nameof(Index), new RouteValueDictionary
        {
            { _optionsSearch, model.Options?.Search },
            { "pageSize", pagerParameters.PageSize },
        });
    }

    /// <summary>
    /// Shows the form for a new key.
    /// </summary>
    /// <returns>The form.</returns>
    [Admin("ai/api-keys/create", "AIApiKeysCreate")]
    public async Task<IActionResult> Create()
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIApiKeys))
        {
            return Forbid();
        }

        return View(new EditCatalogEntryViewModel
        {
            DisplayName = S["New API key"],
            Editor = await _displayManager.BuildEditorAsync(new AIApiKey(), _updateModelAccessor.ModelUpdater, isNew: true),
        });
    }

    /// <summary>
    /// Makes a new key and shows it, the only time a server key is shown.
    /// </summary>
    /// <returns>The key, or the form when it is not valid.</returns>
    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("ai/api-keys/create", "AIApiKeysCreate")]
    public async Task<IActionResult> CreatePost()
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIApiKeys))
        {
            return Forbid();
        }

        var key = new AIApiKey
        {
            ItemId = UniqueId.GenerateId(),
            CreatedUtc = _clock.UtcNow,
            Author = User.Identity?.Name,
        };

        var editor = await _displayManager.UpdateEditorAsync(key, _updateModelAccessor.ModelUpdater, isNew: true);

        if (!ModelState.IsValid)
        {
            return View(new EditCatalogEntryViewModel { DisplayName = S["New API key"], Editor = editor });
        }

        var (value, prefix, hash) = AIApiKeys.Generate(key.Kind);
        key.Prefix = prefix;
        key.Hash = hash;
        key.PublicKey = key.Kind == AIApiKeyKind.Browser ? value : null;

        await _store.CreateAsync(key);

        return View("Created", new AIApiKeyCreatedViewModel { ApiKey = key, Key = value });
    }

    /// <summary>
    /// Shows a key's form.
    /// </summary>
    /// <param name="id">The key.</param>
    /// <returns>The form.</returns>
    [Admin("ai/api-keys/edit/{id}", "AIApiKeysEdit")]
    public async Task<IActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIApiKeys))
        {
            return Forbid();
        }

        var key = await _store.FindByIdAsync(id);

        if (key is null)
        {
            return NotFound();
        }

        return View(new EditCatalogEntryViewModel
        {
            DisplayName = key.Name,
            Editor = await _displayManager.BuildEditorAsync(key, _updateModelAccessor.ModelUpdater, isNew: false),
        });
    }

    /// <summary>
    /// Saves a key.
    /// </summary>
    /// <param name="id">The key.</param>
    /// <returns>A redirect to the list, or the form when it is not valid.</returns>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("ai/api-keys/edit/{id}", "AIApiKeysEdit")]
    public async Task<IActionResult> EditPost(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIApiKeys))
        {
            return Forbid();
        }

        var key = await _store.FindByIdAsync(id);

        if (key is null)
        {
            return NotFound();
        }

        var editor = await _displayManager.UpdateEditorAsync(key, _updateModelAccessor.ModelUpdater, isNew: false);

        if (!ModelState.IsValid)
        {
            return View(new EditCatalogEntryViewModel { DisplayName = key.Name, Editor = editor });
        }

        await _store.UpdateAsync(key);
        await _notifier.SuccessAsync(H["The API key was saved."]);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Deletes a key. Programs using it stop working at once.
    /// </summary>
    /// <param name="id">The key.</param>
    /// <returns>A redirect to the list.</returns>
    [HttpPost]
    [Admin("ai/api-keys/delete/{id}", "AIApiKeysDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIApiKeys))
        {
            return Forbid();
        }

        var key = await _store.FindByIdAsync(id);

        if (key is not null)
        {
            await _store.DeleteAsync(key);
            await _notifier.SuccessAsync(H["The API key was deleted."]);
        }

        return RedirectToAction(nameof(Index));
    }
}
