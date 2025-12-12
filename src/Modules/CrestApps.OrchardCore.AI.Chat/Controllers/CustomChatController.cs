using System.Security.Claims;
using CrestApps.OrchardCore.AI.Chat.Models;
using CrestApps.OrchardCore.AI.Chat.ViewModels;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Entities;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.AI.Chat.Controllers;

public sealed class CustomChatController : Controller
{
    private readonly IAIChatSessionManager _sessionManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IDisplayManager<AIChatSession> _sessionDisplayManager;
    private readonly IDisplayManager<AIChatSessionListOptions> _optionsDisplayManager;
    private readonly INotifier _notifier;
    private readonly AIOptions _aiOptions;
    private readonly IAIProfileManager _profileManager;
    private readonly IAICompletionService _completionService;
    private readonly IAICompletionContextBuilder _contextBuilder;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public CustomChatController(
        IDisplayManager<AIChatSessionListOptions> optionsDisplayManager,
        IAIChatSessionManager sessionManager,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<AIChatSession> sessionDisplayManager,
        INotifier notifier,
        IAIProfileManager profileManager,
        IAICompletionService completionService,
        IAICompletionContextBuilder contextBuilder,
        IOptions<AIOptions> aiOptions,
        IHtmlLocalizer<CustomChatController> htmlLocalizer,
        IStringLocalizer<CustomChatController> stringLocalizer)
    {
        _sessionManager = sessionManager;
        _optionsDisplayManager = optionsDisplayManager;
        _authorizationService = authorizationService;
        _updateModelAccessor = updateModelAccessor;
        _sessionDisplayManager = sessionDisplayManager;
        _notifier = notifier;
        _profileManager = profileManager;
        _completionService = completionService;
        _contextBuilder = contextBuilder;
        _aiOptions = aiOptions.Value;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    [Admin("ai/custom-chat/new-session", "CustomChatNewSession")]
    public IActionResult NewAIChatSession()
    {
        var newSessionId = IdGenerator.GenerateId();

        return RedirectToAction(nameof(Index), new { sessionId = newSessionId });
    }

    [Admin("ai/custom-chat/chat/{sessionId?}")]
    public async Task<IActionResult> Index(string sessionId, [FromServices] IOptions<PagerOptions> pagerOptions)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageCustomChatInstances))
        {
            return Forbid();
        }

        AIChatSession session = null;

        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            session = await _sessionManager.FindAsync(sessionId);
        }

        if (session == null)
        {
            var userSessions = await _sessionManager.PageAsync(1, 50,
                new AIChatSessionQueryContext
                {
                    UserId = CurrentUserId(),
                    Sorted = true
                });

            session = userSessions.Sessions.FirstOrDefault(x => x.As<AIChatInstanceMetadata>()?.IsCustomInstance == true);

            if (session == null)
            {
                return NotFound();
            }
        }

        if (session.As<AIChatInstanceMetadata>()?.IsCustomInstance != true)
        {
            return NotFound();
        }

        var model = new ManageCustomChatInstancesViewModel
        {
            CurrentSession = session,
            Instances = [],
            IsNew = false,
            ChatContent = await _sessionDisplayManager.BuildEditorAsync(
                session,
                _updateModelAccessor.ModelUpdater,
                isNew: false,
                groupId: AIConstants.DisplayGroups.AdminChatSession)
        };

        var sessionResult = await _sessionManager.PageAsync(1, pagerOptions.Value.GetPageSize(),
            new AIChatSessionQueryContext
            {
                UserId = CurrentUserId(),
                Sorted = true,
                ProfileId = session.ProfileId
            });

        model.History = [];

        foreach (var instance in sessionResult.Sessions)
        {
            var summary = await _sessionDisplayManager.BuildDisplayAsync(
                instance, _updateModelAccessor.ModelUpdater, "SummaryAdmin", AIConstants.DisplayGroups.AdminChatSession);

            summary.Properties["Session"] = instance;

            model.History.Add(summary);
        }

        return View(model);
    }


    [Admin("ai/custom-chat/history")]
    public async Task<IActionResult> History(
        PagerParameters pagerParameters,
        AIChatSessionListOptions options,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageCustomChatInstances))
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

        var result = await _sessionManager.PageAsync(page, pagerOptions.Value.GetPageSize(),
            new AIChatSessionQueryContext
            {
                UserId = CurrentUserId(),
                Sorted = true,
                Name = options.SearchText
            });

        var sessions = result.Sessions.Where(x => x.As<AIChatInstanceMetadata>()?.IsCustomInstance == true).ToList();

        var pager = new Pager(pagerParameters, pagerOptions.Value.GetPageSize());

        var pagerShape = await shapeFactory.PagerAsync(pager, result.Count, options.RouteValues);

        var shapeModel = await shapeFactory.CreateAsync<ListChatSessionsViewModel>("AIChatSessionsList_AdminChatSession",
            async vm =>
            {
                vm.ChatSessions = sessions;
                vm.Pager = pagerShape;
                vm.Options = options;
                vm.Header = await _optionsDisplayManager.BuildEditorAsync(options, _updateModelAccessor.ModelUpdater, false);
            });

        return View(shapeModel);
    }

    [HttpPost]
    [ActionName(nameof(History))]
    [Admin("ai/custom-chat/history")]
    public async Task<ActionResult> HistoryPost()
    {
        var options = new AIChatSessionListOptions();

        await _optionsDisplayManager.UpdateEditorAsync(options, _updateModelAccessor.ModelUpdater, false);

        options.RouteValues.TryAdd("q", options.SearchText);

        return RedirectToAction(nameof(History), options.RouteValues);
    }

    [HttpPost]
    [Admin("ai/custom-chat/chat/delete/{sessionId}")]
    public async Task<IActionResult> Delete(string sessionId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.DeleteChatSession))
        {
            return Forbid();
        }

        var session = await _sessionManager.FindAsync(sessionId);

        if (session == null)
        {
            return NotFound();
        }

        if (session.As<AIChatInstanceMetadata>()?.IsCustomInstance != true)
        {
            return NotFound();
        }

        if (await _sessionManager.DeleteAsync(sessionId))
        {
            await _notifier.SuccessAsync(H["Custom chat instance deleted."]);
        }
        else
        {
            await _notifier.ErrorAsync(H["Unable to delete custom chat instance."]);
        }

        return RedirectToAction(nameof(History));
    }

    [HttpPost]
    [Admin("ai/custom-chat/history/delete-all")]
    public async Task<IActionResult> DeleteAll()
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.DeleteAllChatSessions))
        {
            return Forbid();
        }

        var result = await _sessionManager.PageAsync(1, int.MaxValue,
            new AIChatSessionQueryContext
            {
                UserId = CurrentUserId(),
                Sorted = true
            });

        var sessions = result.Sessions.Where(x => x.As<AIChatInstanceMetadata>()?.IsCustomInstance == true).ToList();

        var deleted = 0;

        foreach (var s in sessions)
        {
            if (await _sessionManager.DeleteAsync(s.SessionId))
            {
                deleted++;
            }
        }

        if (deleted > 0)
        {
            await _notifier.SuccessAsync(H["All custom chat instances have been deleted successfully."]);
        }
        else
        {
            await _notifier.InformationAsync(H["No custom chat instances found to delete."]);
        }

        return RedirectToAction(nameof(History));
    }

    private string CurrentUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier);
}
