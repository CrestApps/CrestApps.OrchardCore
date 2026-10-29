using System.Security.Cryptography;
using CrestApps.Core;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Integrations;
using CrestApps.OrchardCore.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
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

namespace CrestApps.OrchardCore.AI.Integrations;

/// <summary>
/// Connections to CRMs and file stores: add one, sign it in with the provider, and remove it.
/// </summary>
[Admin]
[Feature(AIConstants.Feature.Integrations)]
public sealed class AIIntegrationsController : Controller
{
    private const string _optionsSearch = "Options.Search";
    private const string StatePurpose = "CrestApps.AI.Integrations.OAuthState";

    private readonly IAIIntegrationConnectionStore _store;
    private readonly AIIntegrationTokens _tokens;
    private readonly IAuthorizationService _authorizationService;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IDisplayManager<AIIntegrationConnection> _displayManager;
    private readonly ITimeLimitedDataProtector _stateProtector;
    private readonly INotifier _notifier;
    private readonly IClock _clock;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIIntegrationsController"/> class.
    /// </summary>
    /// <param name="store">The connection store.</param>
    /// <param name="tokens">Signs connections in.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="updateModelAccessor">The update model accessor.</param>
    /// <param name="displayManager">The connection display manager.</param>
    /// <param name="dataProtectionProvider">Protects the sign-in state.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIIntegrationsController(
        IAIIntegrationConnectionStore store,
        AIIntegrationTokens tokens,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<AIIntegrationConnection> displayManager,
        IDataProtectionProvider dataProtectionProvider,
        INotifier notifier,
        IClock clock,
        IHtmlLocalizer<AIIntegrationsController> htmlLocalizer,
        IStringLocalizer<AIIntegrationsController> stringLocalizer)
    {
        _store = store;
        _tokens = tokens;
        _authorizationService = authorizationService;
        _updateModelAccessor = updateModelAccessor;
        _displayManager = displayManager;
        _stateProtector = dataProtectionProvider.CreateProtector(StatePurpose).ToTimeLimitedDataProtector();
        _notifier = notifier;
        _clock = clock;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists the connections.
    /// </summary>
    /// <param name="options">The search.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The list.</returns>
    [Admin("ai/integrations", "AIIntegrationsIndex")]
    public async Task<IActionResult> Index(
        CatalogEntryOptions options,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIIntegrations))
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
            List = new AdminListContext<AIIntegrationConnection>
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
    [Admin("ai/integrations", "AIIntegrationsIndex")]
    public async Task<IActionResult> IndexFilterPost(ListCatalogEntryViewModel model, PagerParameters pagerParameters)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIIntegrations))
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
    /// Shows the form for a new connection.
    /// </summary>
    /// <returns>The form.</returns>
    [Admin("ai/integrations/create", "AIIntegrationsCreate")]
    public async Task<IActionResult> Create()
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIIntegrations))
        {
            return Forbid();
        }

        return View(new EditCatalogEntryViewModel
        {
            DisplayName = S["New integration"],
            Editor = await _displayManager.BuildEditorAsync(new AIIntegrationConnection(), _updateModelAccessor.ModelUpdater, isNew: true),
        });
    }

    /// <summary>
    /// Saves a new connection, then signs it in when it uses OAuth.
    /// </summary>
    /// <returns>The provider's sign-in, the list, or the form when it is not valid.</returns>
    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("ai/integrations/create", "AIIntegrationsCreate")]
    public async Task<IActionResult> CreatePost()
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIIntegrations))
        {
            return Forbid();
        }

        var connection = new AIIntegrationConnection { ItemId = UniqueId.GenerateId(), CreatedUtc = _clock.UtcNow };
        var editor = await _displayManager.UpdateEditorAsync(connection, _updateModelAccessor.ModelUpdater, isNew: true);

        if (!ModelState.IsValid)
        {
            return View(new EditCatalogEntryViewModel { DisplayName = S["New integration"], Editor = editor });
        }

        await _store.CreateAsync(connection);

        if (AIIntegrationProviders.UsesOAuth(connection.Provider))
        {
            return RedirectToAction(nameof(Connect), new { id = connection.ItemId });
        }

        await _notifier.SuccessAsync(H["The integration was saved."]);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Shows a connection's form.
    /// </summary>
    /// <param name="id">The connection.</param>
    /// <returns>The form.</returns>
    [Admin("ai/integrations/edit/{id}", "AIIntegrationsEdit")]
    public async Task<IActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIIntegrations))
        {
            return Forbid();
        }

        var connection = await _store.FindByIdAsync(id);

        if (connection is null)
        {
            return NotFound();
        }

        return View(new EditCatalogEntryViewModel
        {
            DisplayName = connection.Name,
            Editor = await _displayManager.BuildEditorAsync(connection, _updateModelAccessor.ModelUpdater, isNew: false),
        });
    }

    /// <summary>
    /// Saves a connection.
    /// </summary>
    /// <param name="id">The connection.</param>
    /// <returns>A redirect to the list, or the form when it is not valid.</returns>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("ai/integrations/edit/{id}", "AIIntegrationsEdit")]
    public async Task<IActionResult> EditPost(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIIntegrations))
        {
            return Forbid();
        }

        var connection = await _store.FindByIdAsync(id);

        if (connection is null)
        {
            return NotFound();
        }

        var editor = await _displayManager.UpdateEditorAsync(connection, _updateModelAccessor.ModelUpdater, isNew: false);

        if (!ModelState.IsValid)
        {
            return View(new EditCatalogEntryViewModel { DisplayName = connection.Name, Editor = editor });
        }

        await _store.UpdateAsync(connection);
        await _notifier.SuccessAsync(H["The integration was saved."]);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Sends the admin to the provider to approve the app.
    /// </summary>
    /// <param name="id">The connection.</param>
    /// <returns>A redirect to the provider.</returns>
    [Admin("ai/integrations/connect/{id}", "AIIntegrationsConnect")]
    public async Task<IActionResult> Connect(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIIntegrations))
        {
            return Forbid();
        }

        var connection = await _store.FindByIdAsync(id);

        if (connection is null || !AIIntegrationProviders.UsesOAuth(connection.Provider))
        {
            return NotFound();
        }

        // The state names the connection and a nonce, and expires, so a forged answer cannot sign anything in.
        var state = _stateProtector.Protect($"{connection.ItemId}|{Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(8))}", TimeSpan.FromMinutes(15));

        return Redirect(AIIntegrationTokens.BuildAuthorizeUrl(connection, CallbackUrl(), state));
    }

    /// <summary>
    /// Takes the provider's answer and keeps the tokens.
    /// </summary>
    /// <param name="code">The code the provider sent.</param>
    /// <param name="state">The state sent with the request.</param>
    /// <param name="error">The provider's error, when the admin refused.</param>
    /// <returns>A redirect to the list.</returns>
    [Admin("ai/integrations/callback", "AIIntegrationsCallback")]
    public async Task<IActionResult> Callback(string code, string state, string error)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIIntegrations))
        {
            return Forbid();
        }

        string connectionId;

        try
        {
            connectionId = _stateProtector.Unprotect(state ?? string.Empty).Split('|')[0];
        }
        catch (CryptographicException)
        {
            await _notifier.ErrorAsync(H["The sign-in took too long or did not come from here. Connect again."]);

            return RedirectToAction(nameof(Index));
        }

        var connection = await _store.FindByIdAsync(connectionId);

        if (connection is null)
        {
            return NotFound();
        }

        if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(code))
        {
            await _notifier.ErrorAsync(H["{0} did not approve the app: {1}", connection.Provider, error]);

            return RedirectToAction(nameof(Index));
        }

        var problem = await _tokens.ExchangeCodeAsync(connection, code, CallbackUrl(), HttpContext.RequestAborted);

        if (problem is not null)
        {
            await _notifier.ErrorAsync(H["{0} did not issue a token: {1}", connection.Provider, problem]);

            return RedirectToAction(nameof(Index));
        }

        connection.ConnectedUtc = _clock.UtcNow;
        connection.ConnectedBy = User.Identity?.Name;
        await _store.UpdateAsync(connection);
        await _notifier.SuccessAsync(H["'{0}' is connected.", connection.Name]);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Deletes a connection. Tools and tasks that use it stop working.
    /// </summary>
    /// <param name="id">The connection.</param>
    /// <returns>A redirect to the list.</returns>
    [HttpPost]
    [Admin("ai/integrations/delete/{id}", "AIIntegrationsDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIIntegrations))
        {
            return Forbid();
        }

        var connection = await _store.FindByIdAsync(id);

        if (connection is not null)
        {
            await _store.DeleteAsync(connection);
            await _notifier.SuccessAsync(H["The integration was deleted."]);
        }

        return RedirectToAction(nameof(Index));
    }

    private string CallbackUrl()
        => Url.Action(nameof(Callback), "AIIntegrations", new { area = AIConstants.Feature.Area }, Request.Scheme);
}
