using System.Security.Claims;
using CrestApps.OrchardCore.OpenAI.Azure.Core;
using CrestApps.OrchardCore.OpenAI.Core;
using CrestApps.OrchardCore.OpenAI.Models;
using CrestApps.OrchardCore.OpenAI.ViewModels;
using CrestApps.OrchardCore.OpenAI.ViewModels.Sessions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using YesSql;

namespace CrestApps.OrchardCore.OpenAI.Controllers;

[Admin]
[Feature(OpenAIConstants.Feature.ChatGPT)]
public sealed class AdminChatController : Controller
{
    private readonly IOpenAIChatProfileManager _profileManager;
    private readonly IOpenAIChatSessionManager _sessionManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly ISession _session;
    private readonly IDisplayManager<OpenAIChatSession> _sessionDisplayManager;
    private readonly IDisplayManager<OpenAIChatListOptions> _optionsDisplayManager;
    private readonly IUpdateModelAccessor _updateModelAccessor;

    internal readonly IStringLocalizer S;

    public AdminChatController(
        IOpenAIChatProfileManager profileManager,
        IOpenAIChatSessionManager sessionManager,
        IAuthorizationService authorizationService,
        ISession session,
        IDisplayManager<OpenAIChatSession> sessionDisplayManager,
        IDisplayManager<OpenAIChatListOptions> optionsDisplayManager,
        IUpdateModelAccessor updateModelAccessor,
        IStringLocalizer<AdminChatController> stringLocalizer
        )
    {
        _profileManager = profileManager;
        _sessionManager = sessionManager;
        _authorizationService = authorizationService;
        _session = session;
        _sessionDisplayManager = sessionDisplayManager;
        _optionsDisplayManager = optionsDisplayManager;
        _updateModelAccessor = updateModelAccessor;
        S = stringLocalizer;
    }

    [Admin("OpenAI/ChatGPT/Session/{profileId}/{sessionId?}", "OpenAIChatGPTSessionsIndex")]
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

        if (!await _authorizationService.AuthorizeAsync(User, OpenAIChatPermissions.QueryAnyAIChatProfile, profile))
        {
            return Forbid();
        }

        var model = new ChatSessionViewModel
        {
            ProfileId = profile.Id,
            History = []
        };

        var userId = CurrentUserId();
        if (!string.IsNullOrEmpty(sessionId))
        {
            var chatSession = await _sessionManager.FindAsync(sessionId);

            if (chatSession == null || chatSession.ProfileId != profile.Id)
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
            var chatSession = new OpenAIChatSession
            {
                ProfileId = profileId,
                UserId = userId,
            };

            model.Content = await _sessionDisplayManager.BuildEditorAsync(chatSession, _updateModelAccessor.ModelUpdater, isNew: true);
        }

        var sessionResult = await _sessionManager.PageAsync(1, pagerOptions.Value.GetPageSize(), new ChatSessionQueryContext
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

    [Admin("OpenAI/ChatGPT/History/{profileId}", "OpenAIChatGPTHistory")]
    public async Task<IActionResult> History(
        string profileId,
        PagerParameters pagerParameters,
        OpenAIChatListOptions options,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        var profile = await _profileManager.FindByIdAsync(profileId);

        if (profile is null)
        {
            return NotFound();
        }

        if (!await _authorizationService.AuthorizeAsync(User, OpenAIChatPermissions.QueryAnyAIChatProfile, profile))
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

        var sessionResult = await _sessionManager.PageAsync(page, pagerOptions.Value.GetPageSize(), new ChatSessionQueryContext
        {
            ProfileId = profileId,
            Name = options.SearchText
        });

        var itemsPerPage = pagerOptions.Value.MaxPagedCount > 0
            ? pagerOptions.Value.MaxPagedCount
            : sessionResult.Count;

        var pager = new Pager(pagerParameters, pagerOptions.Value.GetPageSize());

        var pagerShape = await shapeFactory.PagerAsync(pager, itemsPerPage, options.RouteValues);

        var shapeViewModel = await shapeFactory.CreateAsync<ListChatSessionsViewModel>("OpenAIChatSessionsList", async viewModel =>
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
        var options = new OpenAIChatListOptions();
        // Evaluate the values provided in the form post and map them to the filter result and route values.
        await _optionsDisplayManager.UpdateEditorAsync(options, _updateModelAccessor.ModelUpdater, false);

        // The route value must always be added after the editors have updated the models.
        options.RouteValues.TryAdd("q", options.SearchText);
        options.RouteValues.TryAdd("profileId", profileId);

        return RedirectToAction(nameof(History), options.RouteValues);
    }

    [Admin("OpenAI/ChatGPT/Chat/{profileId}/", "OpenAIChatGPTNewChat")]
    public IActionResult Chat(string profileId)
        => RedirectToAction(nameof(Index), new { profileId });

    private string CurrentUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier);
}
