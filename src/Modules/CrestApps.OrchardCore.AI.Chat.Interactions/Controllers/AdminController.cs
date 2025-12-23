using System.Security.Claims;
using CrestApps.OrchardCore.AI.Chat.Interactions.ViewModels;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Core.Models;
using CrestApps.OrchardCore.Services;
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
using OrchardCore.Navigation;
using OrchardCore.Routing;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Controllers;

[Admin("ai/chat/interactions/{action}/{itemId?}", "ChatInteractions{action}")]
public sealed class AdminController : Controller
{
    private const string _optionsSearch = "Options.Search";

    private readonly ISourceCatalogManager<ChatInteraction> _interactionManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly IDisplayManager<ChatInteraction> _interactionDisplayManager;
    private readonly IDisplayManager<ChatInteractionListOptions> _optionsDisplayManager;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly INotifier _notifier;
    private readonly AIOptions _aiOptions;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public AdminController(
        ISourceCatalogManager<ChatInteraction> interactionManager,
        IAuthorizationService authorizationService,
        IDisplayManager<ChatInteraction> interactionDisplayManager,
        IDisplayManager<ChatInteractionListOptions> optionsDisplayManager,
        IUpdateModelAccessor updateModelAccessor,
        INotifier notifier,
        IOptions<AIOptions> aiOptions,
        IHtmlLocalizer<AdminController> htmlLocalizer,
        IStringLocalizer<AdminController> stringLocalizer)
    {
        _interactionManager = interactionManager;
        _authorizationService = authorizationService;
        _interactionDisplayManager = interactionDisplayManager;
        _optionsDisplayManager = optionsDisplayManager;
        _updateModelAccessor = updateModelAccessor;
        _notifier = notifier;
        _aiOptions = aiOptions.Value;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    [Admin("ai/chat-interactions", "AIInteractionsIndex")]
    public async Task<IActionResult> Index(
        string itemId,
        string source,
        CatalogEntryOptions options,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ListChatInteractions))
        {
            return Forbid();
        }

        var model = new ChatInteractionViewModel
        {
            History = [],
            Sources = _aiOptions.ProfileSources.Select(x => x.Key).Order(),
        };

        // Maintain previous route data when generating page links.
        var routeData = new RouteData();

        if (!string.IsNullOrEmpty(options.Search))
        {
            routeData.Values.TryAdd(_optionsSearch, options.Search);
        }

        ChatInteraction interaction;
        string currentSource = null;

        if (!string.IsNullOrEmpty(itemId))
        {
            interaction = await _interactionManager.FindByIdAsync(itemId);

            if (interaction is null)
            {
                return NotFound();
            }

            if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.EditChatInteractions, interaction))
            {
                return Forbid();
            }

            model.ItemId = itemId;
            model.Source = interaction.Source;
            currentSource = interaction.Source;
            model.Content = await _interactionDisplayManager.BuildEditorAsync(interaction, _updateModelAccessor.ModelUpdater, isNew: false);
        }
        else if (!string.IsNullOrEmpty(source))
        {
            if (!_aiOptions.ProfileSources.TryGetValue(source, out var provider))
            {
                await _notifier.ErrorAsync(H["Unable to find a source that can handle '{0}'.", source]);

                return RedirectToAction(nameof(Index));
            }

            interaction = await _interactionManager.NewAsync(source);

            if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.EditChatInteractions, interaction))
            {
                return Forbid();
            }

            // Save the interaction immediately so it can be used by the SignalR hub
            await _interactionManager.CreateAsync(interaction);

            model.ItemId = interaction.ItemId;
            model.Source = source;
            currentSource = source;
            model.Content = await _interactionDisplayManager.BuildEditorAsync(interaction, _updateModelAccessor.ModelUpdater, isNew: true);
        }

        var queryContext = new ChatInteractionQueryContext();

        if (!string.IsNullOrEmpty(currentSource))
        {
            queryContext.Source = currentSource;
        }

        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ListChatInteractionsForOthers))
        {
            // At this point the user cannot view all interactions.
            queryContext.UserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        }

        var interactionResult = await _interactionManager.PageAsync(1, pagerOptions.Value.GetPageSize(), queryContext);

        foreach (var item in interactionResult.Entries)
        {
            var summary = await _interactionDisplayManager.BuildDisplayAsync(item, _updateModelAccessor.ModelUpdater, "SummaryAdmin");
            summary.Properties["Interaction"] = item;

            model.History.Add(summary);
        }

        return View(model);
    }

    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("ai/chat-interactions", "AIInteractionsIndex")]
    public ActionResult IndexFilterPost(ListCatalogEntryViewModel model)
    {
        return RedirectToAction(nameof(Index), new RouteValueDictionary
        {
            { _optionsSearch, model.Options?.Search },
        });
    }

    /*
    public async Task<IActionResult> History(
        PagerParameters pagerParameters,
        ChatInteractionListOptions options,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageChatInteractions))
        {
            return Forbid();
        }

        if (!string.IsNullOrWhiteSpace(options.SearchText))
        {
            options.RouteValues.TryAdd("q", options.SearchText);
        }

        var page = 1;

        if (pagerParameters.Page.HasValue && pagerParameters.Page.Value > 0)
        {
            page = pagerParameters.Page.Value;
        }

        var interactionResult = await _interactionManager.PageAsync(page, pagerOptions.Value.GetPageSize(), new ChatInteractionQueryContext
        {
            Title = options.SearchText
        });

        var itemsPerPage = pagerOptions.Value.MaxPagedCount > 0
            ? pagerOptions.Value.MaxPagedCount
            : interactionResult.Count;

        var pager = new Pager(pagerParameters, pagerOptions.Value.GetPageSize());

        var pagerShape = await shapeFactory.PagerAsync(pager, itemsPerPage, options.RouteValues);

        var shapeViewModel = await shapeFactory.CreateAsync<ListChatInteractionsViewModel>("ChatInteractionsList", async viewModel =>
        {
            viewModel.Interactions = interactionResult.Entries;
            viewModel.Pager = pagerShape;
            viewModel.Options = options;
            viewModel.Header = await _optionsDisplayManager.BuildEditorAsync(options, _updateModelAccessor.ModelUpdater, false);
        });

        return View(shapeViewModel);
    }

    [HttpPost]
    [ActionName(nameof(History))]
    public async Task<ActionResult> HistoryPost()
    {
        var options = new ChatInteractionListOptions();
        await _optionsDisplayManager.UpdateEditorAsync(options, _updateModelAccessor.ModelUpdater, false);

        options.RouteValues.TryAdd("q", options.SearchText);

        return RedirectToAction(nameof(History), options.RouteValues);
    }
    */

    public IActionResult Chat()
        => RedirectToAction(nameof(Index));

    [HttpPost]
    public async Task<IActionResult> Delete(string itemId)
    {
        var interaction = await _interactionManager.FindByIdAsync(itemId);

        if (interaction is null)
        {
            return NotFound();
        }

        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.DeleteChatInteraction, interaction))
        {
            return Forbid();
        }

        if (await _interactionManager.DeleteAsync(interaction))
        {
            await _notifier.SuccessAsync(H["Chat interaction has been deleted successfully."]);
        }
        else
        {
            await _notifier.ErrorAsync(H["Unable to delete the chat interaction."]);
        }

        return RedirectToAction(nameof(Index));
    }
}
