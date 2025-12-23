using CrestApps.OrchardCore.AI.Chat.Interactions.ViewModels;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Controllers;

[Admin("ai/chat/interactions/{action}/{interactionId?}", "ChatInteractions{action}")]
public sealed class AdminController : Controller
{
    private readonly IChatInteractionManager _interactionManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly IDisplayManager<ChatInteraction> _interactionDisplayManager;
    private readonly IDisplayManager<ChatInteractionListOptions> _optionsDisplayManager;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly INotifier _notifier;
    private readonly AIOptions _aiOptions;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public AdminController(
        IChatInteractionManager interactionManager,
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

    public async Task<IActionResult> Index(
        string interactionId,
        string source,
        [FromServices] IOptions<PagerOptions> pagerOptions)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageChatInteractions))
        {
            return Forbid();
        }

        var model = new ChatInteractionViewModel
        {
            History = [],
            Sources = _aiOptions.ProfileSources.Select(x => x.Key).Order(),
        };

        ChatInteraction interaction;

        if (!string.IsNullOrEmpty(interactionId))
        {
            interaction = await _interactionManager.FindAsync(interactionId);

            if (interaction == null)
            {
                return NotFound();
            }

            model.InteractionId = interactionId;
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

            // Save the interaction immediately so it can be used by the SignalR hub
            await _interactionManager.SaveAsync(interaction);

            model.InteractionId = interaction.InteractionId;
            model.Content = await _interactionDisplayManager.BuildEditorAsync(interaction, _updateModelAccessor.ModelUpdater, isNew: true);
        }
        else
        {
            // Show source selection dialog
            return View(model);
        }

        var interactionResult = await _interactionManager.PageAsync(1, pagerOptions.Value.GetPageSize(), new ChatInteractionQueryContext());

        foreach (var item in interactionResult.Interactions)
        {
            var summary = await _interactionDisplayManager.BuildDisplayAsync(item, _updateModelAccessor.ModelUpdater, "SummaryAdmin");
            summary.Properties["Interaction"] = item;

            model.History.Add(summary);
        }

        return View(model);
    }

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
            viewModel.Interactions = interactionResult.Interactions;
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

    public IActionResult Chat()
        => RedirectToAction(nameof(Index));

    [HttpPost]
    public async Task<IActionResult> Delete(string interactionId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.DeleteChatInteraction))
        {
            return Forbid();
        }

        var interaction = await _interactionManager.FindAsync(interactionId);

        if (interaction == null)
        {
            return NotFound();
        }

        if (await _interactionManager.DeleteAsync(interactionId))
        {
            await _notifier.SuccessAsync(H["Chat interaction has been deleted successfully."]);
        }
        else
        {
            await _notifier.ErrorAsync(H["Unable to delete the chat interaction."]);
        }

        return RedirectToAction(nameof(History));
    }

    [HttpPost]
    public async Task<IActionResult> DeleteAll()
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.DeleteAllChatInteractions))
        {
            return Forbid();
        }

        var count = await _interactionManager.DeleteAllAsync();

        if (count > 0)
        {
            await _notifier.SuccessAsync(H["All chat interactions have been deleted successfully."]);
        }
        else
        {
            await _notifier.InformationAsync(H["No chat interactions found to delete."]);
        }

        return RedirectToAction(nameof(Index));
    }
}
