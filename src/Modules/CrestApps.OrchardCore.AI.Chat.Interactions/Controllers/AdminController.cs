using System.Security.Claims;
using System.Text.RegularExpressions;
using CrestApps.OrchardCore.AI.Chat.Interactions.ViewModels;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Core.Models;
using CrestApps.OrchardCore.Services;
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

[Admin("ai/chat/interactions/{action}/{itemId?}", "ChatInteractions{action}")]
public sealed class AdminController : Controller
{
    private const string _optionsSearch = "Options.Search";

    private readonly ISourceCatalogManager<ChatInteraction> _interactionManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly IDisplayManager<ChatInteraction> _interactionDisplayManager;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly INotifier _notifier;
    private readonly AIOptions _aiOptions;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public AdminController(
        ISourceCatalogManager<ChatInteraction> interactionManager,
        IAuthorizationService authorizationService,
        IDisplayManager<ChatInteraction> interactionDisplayManager,
        IUpdateModelAccessor updateModelAccessor,
        INotifier notifier,
        IOptions<AIOptions> aiOptions,
        IHtmlLocalizer<AdminController> htmlLocalizer,
        IStringLocalizer<AdminController> stringLocalizer)
    {
        _interactionManager = interactionManager;
        _authorizationService = authorizationService;
        _interactionDisplayManager = interactionDisplayManager;
        _updateModelAccessor = updateModelAccessor;
        _notifier = notifier;
        _aiOptions = aiOptions.Value;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

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

        var viewModel = new ListSourceCatalogEntryViewModel<ChatInteraction>
        {
            Models = [],
            Options = options,
            Pager = await shapeFactory.PagerAsync(pager, result.Count, routeData),
            Sources = _aiOptions.ProfileSources.Select(x => x.Key).Order(),
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

    [Admin("ai/chat/interaction/chat/{source}/{itemId?}", "ChatInteractionsChat")]
    public async Task<ActionResult> Chat(string source, string itemId)
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

            // Save the interaction immediately so it can be used by the SignalR hub.
            await _interactionManager.CreateAsync(interaction);

            isNew = true;
        }

        var model = new EditChatInteractionEntryViewModel
        {
            ItemId = interaction.ItemId,
            Source = interaction.Source,
            DisplayName = isNew ? _aiOptions.ProfileSources[interaction.Source].DisplayName : (interaction.Title ?? "Untitled"),
            Editor = await _interactionDisplayManager.BuildEditorAsync(interaction, _updateModelAccessor.ModelUpdater, isNew: isNew),
        };

        return View(model);
    }

    [Admin("ai/chat/interaction/new-chat/{source}", "NewInteractionsChat")]
    public async Task<ActionResult> New(string source)
        => RedirectToAction(nameof(Chat), new { source });

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

        var clonedInteraction = await _interactionManager.NewAsync(interaction.Source, interaction.Properties);
        clonedInteraction.Title = GetNextTitle(interaction.Title);
        clonedInteraction.DeploymentId = interaction.DeploymentId;
        clonedInteraction.ConnectionName = interaction.ConnectionName;
        clonedInteraction.SystemMessage = interaction.SystemMessage;
        clonedInteraction.Temperature = interaction.Temperature;
        clonedInteraction.TopP = interaction.TopP;
        clonedInteraction.FrequencyPenalty = interaction.FrequencyPenalty;
        clonedInteraction.PresencePenalty = interaction.PresencePenalty;
        clonedInteraction.MaxTokens = interaction.MaxTokens;
        clonedInteraction.PastMessagesCount = interaction.PastMessagesCount;
        clonedInteraction.DocumentTopN = interaction.DocumentTopN;
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
            source = clonedInteraction.Source,
            itemId = clonedInteraction.ItemId,
        });
    }

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
