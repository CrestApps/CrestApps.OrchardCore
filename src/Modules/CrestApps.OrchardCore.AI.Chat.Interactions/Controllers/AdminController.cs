using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Chat.Interactions.ViewModels;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Core.Models;
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

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Controllers;

/// <summary>
/// Provides endpoints for managing admin resources.
/// </summary>
[Admin("ai/chat/interactions/{action}/{itemId?}", "ChatInteractions{action}")]
public sealed class AdminController : Controller
{
    private const string _optionsSearch = "Options.Search";

    private readonly ICatalogManager<ChatInteraction> _interactionManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly IDisplayManager<ChatInteraction> _interactionDisplayManager;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminController"/> class.
    /// </summary>
    /// <param name="interactionManager">The interaction manager.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="interactionDisplayManager">The interaction display manager.</param>
    /// <param name="updateModelAccessor">The update model accessor.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The html localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AdminController(
        ICatalogManager<ChatInteraction> interactionManager,
        IAuthorizationService authorizationService,
        IDisplayManager<ChatInteraction> interactionDisplayManager,
        IUpdateModelAccessor updateModelAccessor,
        INotifier notifier,
        IHtmlLocalizer<AdminController> htmlLocalizer,
        IStringLocalizer<AdminController> stringLocalizer)
    {
        _interactionManager = interactionManager;
        _authorizationService = authorizationService;
        _interactionDisplayManager = interactionDisplayManager;
        _updateModelAccessor = updateModelAccessor;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Performs the index operation.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    [Admin("ai/chat-interactions", "AIInteractionsIndex")]
    public async Task<IActionResult> Index(
        CatalogEntryOptions options,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ListChatInteractions))
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value.GetPageSize());

        var queryContext = new ChatInteractionQueryContext
        {
            Name = options.Search,
            Sorted = true,
        };

        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ListChatInteractionsForOthers))
        {
            // User cannot view all interactions.
            queryContext.UserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        }

        var result = await _interactionManager.PageAsync(pager.Page, pager.PageSize, queryContext);

        // Maintain previous route data when generating page links.
        var routeData = new RouteData();

        if (!string.IsNullOrEmpty(options.Search))
        {
            routeData.Values.TryAdd(_optionsSearch, options.Search);
        }

        var viewModel = new ListCatalogEntryViewModel<CatalogEntryViewModel<ChatInteraction>>
        {
            Models = [],
            Options = options,
            Pager = await shapeFactory.PagerAsync(pager, result.Count, routeData),
        };

        // Build display shapes for each interaction
        viewModel.Models = (await Task.WhenAll(result.Entries.Select(async model =>
        new CatalogEntryViewModel<ChatInteraction>
        {
            Model = model,
            Shape = await _interactionDisplayManager.BuildDisplayAsync(model, _updateModelAccessor.ModelUpdater, "SummaryAdmin")
        }))).ToList();

        viewModel.Options.BulkActions =
        [
            new SelectListItem(S["Delete"], nameof(CatalogEntryAction.Remove)),
        ];

        return View(viewModel);
    }

    /// <summary>
    /// Performs the index filter post operation.
    /// </summary>
    /// <param name="model">The model.</param>
    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("ai/chat-interactions", "AIInteractionsIndex")]
    public async Task<ActionResult> IndexFilterPost(ListCatalogEntryViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ListChatInteractions))
        {
            return Forbid();
        }

        return RedirectToAction(nameof(Index), new RouteValueDictionary
        {
            { _optionsSearch, model.Options?.Search },
        });
    }

    /// <summary>
    /// Performs the index post operation.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <param name="itemIds">The item ids.</param>
    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.BulkAction")]
    [Admin("ai/chat-interactions", "AIInteractionsIndex")]
    public async Task<ActionResult> IndexPost(CatalogEntryOptions options, IEnumerable<string> itemIds)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ListChatInteractions))
        {
            return Forbid();
        }

        if (itemIds?.Count() > 0)
        {
            switch (options.BulkAction)
            {
                case CatalogEntryAction.None:
                    break;
                case CatalogEntryAction.Remove:
                    var counter = 0;
                    foreach (var id in itemIds)
                    {
                        var interaction = await _interactionManager.FindByIdAsync(id);

                        if (interaction == null)
                        {
                            continue;
                        }

                        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.DeleteChatInteraction, interaction))
                        {
                            continue;
                        }

                        if (await _interactionManager.DeleteAsync(interaction))
                        {
                            counter++;
                        }
                    }
                    if (counter == 0)
                    {
                        await _notifier.WarningAsync(H["No chat interactions were removed."]);
                    }
                    else
                    {
                        await _notifier.SuccessAsync(H.Plural(counter, "1 chat interaction has been removed successfully.", "{0} chat interactions have been removed successfully."));
                    }
                    break;
                default:
                    return BadRequest();
            }
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Performs the chat operation.
    /// </summary>
    /// <param name="itemId">The item id.</param>
    [Admin("ai/chat/interaction/chat/{itemId?}", "ChatInteractionsChat")]
    public async Task<ActionResult> Chat(string itemId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.EditChatInteractions))
        {
            return Forbid();
        }

        ChatInteraction interaction;
        bool isNew;

        if (!string.IsNullOrEmpty(itemId))
        {
            // Editing existing interaction.
            interaction = await _interactionManager.FindByIdAsync(itemId);

            if (interaction == null)
            {
                return NotFound();
            }

            if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.EditChatInteractions, interaction))
            {
                return Forbid();
            }

            isNew = false;
        }
        else
        {
            // Creating new interaction.
            interaction = await _interactionManager.NewAsync();

            if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.EditChatInteractions, interaction))
            {
                return Forbid();
            }

            // Save the interaction immediately so it can be used by the SignalR hub.
            await _interactionManager.CreateAsync(interaction);

            isNew = true;
        }

        var model = new EditChatInteractionEntryViewModel
        {
            ItemId = interaction.ItemId,
            DisplayName = isNew ? "New Chat" : (interaction.Title ?? "Untitled"),
            Editor = await _interactionDisplayManager.BuildEditorAsync(interaction, _updateModelAccessor.ModelUpdater, isNew: isNew),
        };

        return View(model);
    }

    /// <summary>
    /// Performs the new operation.
    /// </summary>
    [Admin("ai/chat/interaction/new-chat", "NewInteractionsChat")]
    public ActionResult New()
        => RedirectToAction(nameof(Chat));

    /// <summary>
    /// Performs the clone operation.
    /// </summary>
    /// <param name="itemId">The item id.</param>
    [Admin("ai/chat/interaction/clone-chat/{itemId}", "CloneInteractionsChat")]
    public async Task<ActionResult> Clone(string itemId)
    {
        var interaction = await _interactionManager.FindByIdAsync(itemId);

        if (interaction is null)
        {
            return NotFound();
        }

        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.EditChatInteractions, interaction))
        {
            return Forbid();
        }

        var clonedInteraction = await _interactionManager.NewAsync(JsonSerializer.SerializeToNode(interaction.Properties));
        clonedInteraction.Title = GetNextTitle(interaction.Title);
        clonedInteraction.ChatDeploymentName = interaction.ChatDeploymentName;
        clonedInteraction.ConnectionName = interaction.ConnectionName;
        clonedInteraction.SystemMessage = interaction.SystemMessage;
        clonedInteraction.Temperature = interaction.Temperature;
        clonedInteraction.TopP = interaction.TopP;
        clonedInteraction.FrequencyPenalty = interaction.FrequencyPenalty;
        clonedInteraction.PresencePenalty = interaction.PresencePenalty;
        clonedInteraction.MaxTokens = interaction.MaxTokens;
        clonedInteraction.PastMessagesCount = interaction.PastMessagesCount;
        clonedInteraction.ToolNames = interaction.ToolNames.ToList();
        clonedInteraction.McpConnectionIds = interaction.McpConnectionIds.ToList();
        clonedInteraction.Documents = interaction.Documents.ToList();
        clonedInteraction.DocumentIndex = interaction.Documents.Count; // Set the document index based on the cloned documents.

        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.EditChatInteractions, clonedInteraction))
        {
            return Forbid();
        }

        // Save the interaction immediately so it can be used by the SignalR hub.
        await _interactionManager.CreateAsync(clonedInteraction);

        return RedirectToAction(nameof(Chat), new
        {
            itemId = clonedInteraction.ItemId,
        });
    }

    /// <summary>
    /// Removes the .
    /// </summary>
    /// <param name="itemId">The item id.</param>
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

    private static readonly Regex PostfixRegex = new Regex(@"\s*\((\d+)\)$", RegexOptions.Compiled);

    private static string GetNextTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return "Untitled (1)";
        }

        var match = PostfixRegex.Match(title);

        if (match.Success)
        {
            // Extract number and increment
            var number = int.Parse(match.Groups[1].Value);
            var baseTitle = title.Substring(0, match.Index).TrimEnd();

            return $"{baseTitle} ({number + 1})";
        }

        // No postfix found → start at (1)
        return $"{title} (1)";
    }
}
