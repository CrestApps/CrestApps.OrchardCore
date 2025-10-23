using System.Security.Claims;
using CrestApps.OrchardCore.AI.Chat.Models;
using CrestApps.OrchardCore.AI.Chat.ViewModels;
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

namespace CrestApps.OrchardCore.AI.Chat.Controllers;

[Admin]
public sealed class AdminController : Controller
{
    private readonly IAIProfileManager _profileManager;
    private readonly IAIChatSessionManager _sessionManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly IDisplayManager<AIChatSession> _sessionDisplayManager;
    private readonly IDisplayManager<AIChatSessionListOptions> _optionsDisplayManager;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly AIOptions _aiOptions;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public AdminController(
        IAIProfileManager profileManager,
        IAIChatSessionManager sessionManager,
        IAuthorizationService authorizationService,
        IDisplayManager<AIChatSession> sessionDisplayManager,
        IDisplayManager<AIChatSessionListOptions> optionsDisplayManager,
        IUpdateModelAccessor updateModelAccessor,
        IOptions<AIOptions> aiOptions,
        INotifier notifier,
        IHtmlLocalizer<AdminController> htmlLocalizer,
        IStringLocalizer<AdminController> stringLocalizer
        )
    {
        _profileManager = profileManager;
        _sessionManager = sessionManager;
        _authorizationService = authorizationService;
        _sessionDisplayManager = sessionDisplayManager;
        _optionsDisplayManager = optionsDisplayManager;
        _updateModelAccessor = updateModelAccessor;
        _aiOptions = aiOptions.Value;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    [Admin("ai/chat/session/{profileId}/{sessionId?}", "AIChatSessionsIndex")]
    public async Task<IActionResult> Index(
        string profileId,
        string sessionId,
        [FromServices] IOptions<PagerOptions> pagerOptions)
    {
        var profile = await _profileManager.FindByIdAsync(profileId);

        if (profile is null)
        {
            return NotFound();
        }

        if (!_aiOptions.Clients.TryGetValue(profile.Source, out var clientOptions))
        {
            return NotFound();
        }

        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.QueryAnyAIProfile, profile))
        {
            return Forbid();
        }

        var model = new ChatSessionViewModel
        {
            ProfileId = profile.ItemId,
            History = []
        };

        var userId = CurrentUserId();
        if (!string.IsNullOrEmpty(sessionId))
        {
            var chatSession = await _sessionManager.FindAsync(sessionId);

            if (chatSession == null || chatSession.ProfileId != profile.ItemId)
            {
                return NotFound();
            }

            if (chatSession.UserId != userId)
            {
                return Forbid();
            }

            model.SessionId = sessionId;
            model.Content = await _sessionDisplayManager.BuildEditorAsync(chatSession, _updateModelAccessor.ModelUpdater, isNew: false);
        }
        else
        {
            var chatSession = new AIChatSession
            {
                ProfileId = profileId,
                UserId = userId,
            };

            model.Content = await _sessionDisplayManager.BuildEditorAsync(chatSession, _updateModelAccessor.ModelUpdater, isNew: true);
        }

        var sessionResult = await _sessionManager.PageAsync(1, pagerOptions.Value.GetPageSize(), new AIChatSessionQueryContext
        {
            ProfileId = profileId,
        });

        foreach (var session in sessionResult.Sessions)
        {
            var summary = await _sessionDisplayManager.BuildDisplayAsync(session, _updateModelAccessor.ModelUpdater, "SummaryAdmin");
            summary.Properties["Session"] = session;

            model.History.Add(summary);
        }

        return View(model);
    }

    [Admin("ai/chat/history/{profileId}", "AIChatHistory")]
    public async Task<IActionResult> History(
        string profileId,
        PagerParameters pagerParameters,
        AIChatSessionListOptions options,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        var profile = await _profileManager.FindByIdAsync(profileId);

        if (profile is null)
        {
            return NotFound();
        }

        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.QueryAnyAIProfile, profile))
        {
            return Forbid();
        }

        if (!string.IsNullOrWhiteSpace(options.SearchText))
        {
            // Populate route values to maintain previous route data when generating page links.
            options.RouteValues.TryAdd("q", options.SearchText);
        }

        var page = 1;

        if (pagerParameters.Page.HasValue && pagerParameters.Page.Value > 0)
        {
            page = pagerParameters.Page.Value;
        }

        var sessionResult = await _sessionManager.PageAsync(page, pagerOptions.Value.GetPageSize(), new AIChatSessionQueryContext
        {
            ProfileId = profileId,
            Name = options.SearchText
        });

        var itemsPerPage = pagerOptions.Value.MaxPagedCount > 0
            ? pagerOptions.Value.MaxPagedCount
            : sessionResult.Count;

        var pager = new Pager(pagerParameters, pagerOptions.Value.GetPageSize());

        var pagerShape = await shapeFactory.PagerAsync(pager, itemsPerPage, options.RouteValues);

        var shapeViewModel = await shapeFactory.CreateAsync<ListChatSessionsViewModel>("AIChatSessionsList", async viewModel =>
        {
            viewModel.ProfileId = profileId;
            viewModel.ChatSessions = sessionResult.Sessions;
            viewModel.Pager = pagerShape;
            viewModel.Options = options;
            viewModel.Header = await _optionsDisplayManager.BuildEditorAsync(options, _updateModelAccessor.ModelUpdater, false);
        });

        return View(shapeViewModel);
    }

    [HttpPost]
    [ActionName(nameof(History))]
    public async Task<ActionResult> HistoryPost(string profileId)
    {
        var options = new AIChatSessionListOptions();
        // Evaluate the values provided in the form post and map them to the filter result and route values.
        await _optionsDisplayManager.UpdateEditorAsync(options, _updateModelAccessor.ModelUpdater, false);

        // The route value must always be added after the editors have updated the models.
        options.RouteValues.TryAdd("q", options.SearchText);
        options.RouteValues.TryAdd("profileId", profileId);

        return RedirectToAction(nameof(History), options.RouteValues);
    }

    [Admin("ai/chat/interact/{profileId}/", "AIChatNewSession")]
    public IActionResult Chat(string profileId)
        => RedirectToAction(nameof(Index), new { profileId });

    [Admin("ai/chat/session/delete/{sessionId}", "DeleteChatSession")]
    public async Task<IActionResult> Delete(string sessionId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.DeleteChatSession))
        {
            return Forbid();
        }

        var chatSession = await _sessionManager.FindAsync(sessionId);

        if (chatSession == null)
        {
            return NotFound();
        }

        var profile = await _profileManager.FindByIdAsync(chatSession.ProfileId);

        if (profile == null)
        {
            return NotFound();
        }

        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.QueryAnyAIProfile, profile))
        {
            return Forbid();
        }

        if (await _sessionManager.DeleteAsync(sessionId))
        {
            await _notifier.SuccessAsync(H["Chat session has been deleted successfully."]);
        }
        else
        {
            await _notifier.ErrorAsync(H["Unable to delete the chat session."]);
        }

        return RedirectToAction(nameof(History), new { profileId = chatSession.ProfileId });
    }

    [HttpPost]
    [Admin("ai/chat/session/delete-all/{profileId}", "DeleteAllChatSessions")]
    public async Task<IActionResult> DeleteAll(string profileId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.DeleteAllChatSessions))
        {
            return Forbid();
        }

        var profile = await _profileManager.FindByIdAsync(profileId);

        if (profile == null)
        {
            return NotFound();
        }

        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.QueryAnyAIProfile, profile))
        {
            return Forbid();
        }

        var count = await _sessionManager.DeleteAllAsync(profileId);

        if (count > 0)
        {
            await _notifier.SuccessAsync(H["All chat sessions have been deleted successfully."]);
        }
        else
        {
            await _notifier.InformationAsync(H["No chat sessions found to delete."]);
        }

        return RedirectToAction(nameof(Index), new { profileId });
    }

    private string CurrentUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier);
}
