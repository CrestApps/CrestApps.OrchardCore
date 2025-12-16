using System.Security.Claims;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Core.Models;
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
using static CrestApps.OrchardCore.AI.Core.AIConstants;

namespace CrestApps.OrchardCore.AI.Chat.Controllers;

[Admin]
public sealed class CustomChatSettingsController : Controller
{
    private readonly IAIChatSessionManager _sessionManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IDisplayManager<AIChatSession> _sessionDisplayManager;
    private readonly INotifier _notifier;

    private readonly AIOptions _aiOptions;

    private readonly IAIProfileManager _profileManager;

    private readonly IAICompletionService _completionService;
    private readonly IAICompletionContextBuilder _contextBuilder;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public CustomChatSettingsController(
        IAIProfileManager profileManager,
        IAICompletionService completionService,
        IAICompletionContextBuilder contextBuilder,
        IOptions<AIOptions> aiOptions,
        IAIChatSessionManager sessionManager,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<AIChatSession> sessionDisplayManager,
        INotifier notifier,
        IHtmlLocalizer<CustomChatSettingsController> htmlLocalizer,
        IStringLocalizer<CustomChatSettingsController> stringLocalizer
        )
    {
        _profileManager = profileManager;
        _completionService = completionService;
        _contextBuilder = contextBuilder;
        _sessionManager = sessionManager;
        _aiOptions = aiOptions.Value;
        _authorizationService = authorizationService;
        _updateModelAccessor = updateModelAccessor;
        _sessionDisplayManager = sessionDisplayManager;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    [Admin("ai/custom-chat")]
    public async Task<IActionResult> Index()
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageCustomChatInstances))
        {
            return Forbid();
        }

        // thisa model wont work for custom chat sessions only
        var sessions = await _sessionManager.PageAsync(1, 100, new AIChatSessionQueryContext
        {
            UserId = CurrentUserId(),
        });

        var customInstances = sessions.Sessions
            .Where(x => x.As<AIChatInstanceMetadata>()?.IsCustomInstance == true)
            .ToList();

        var viewModel = new ListCatalogEntryViewModel<AIChatSession>
        {
            // why do we care for model [] if we have custom models?
            Models = [],
            CustomModels = []
        };

        foreach (var session in customInstances)
        {
            var shape = await _sessionDisplayManager.BuildDisplayAsync(session, _updateModelAccessor.ModelUpdater, ShapeLocations.SummaryAdmin, DisplayGroups.AICustomChatSession);

            viewModel.CustomModels.Add(new CatalogEntryViewModel<AIChatSession>
            {
                Model = session,
                Shape = shape
            });
        }

        return View(viewModel);
    }

    [Admin("ai/custom-chat/create")]
    public async Task<IActionResult> Create()
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageCustomChatInstances))
        {
            return Forbid();
        }

        var userId = CurrentUserId();

        var session = new AIChatSession
        {
            // this is not how we make IDs Mike
            SessionId = IdGenerator.GenerateId(),
            UserId = userId,
            CreatedUtc = DateTime.UtcNow,
            // we no longer use profiles for custom instances
            // ProfileId = "custom-" + Guid.NewGuid().ToString("N") // Placeholder profile ID
        };

        // Mark as custom instance
        session.Put(new AIChatInstanceMetadata { IsCustomInstance = true });

        var model = new EditCatalogEntryViewModel
        {
            DisplayName = S["New Custom Chat Instance"],
            Editor = await _sessionDisplayManager.BuildEditorAsync(session, _updateModelAccessor.ModelUpdater, isNew: true, groupId: DisplayGroups.AICustomChatSession),
        };

        return View(model);
    }

    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("ai/custom-chat/create")]
    public async Task<IActionResult> CreatePost()
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageCustomChatInstances))
        {
            return Forbid();
        }

        var userId = CurrentUserId();

        var session = new AIChatSession
        {
            SessionId = IdGenerator.GenerateId(),
            UserId = userId,
            CreatedUtc = DateTime.UtcNow,
            // wew dont use profiles 
            //  ProfileId = "custom-" + Guid.NewGuid().ToString("N"),
        };

        // Mark as custom instance
        session.Put(new AIChatInstanceMetadata { IsCustomInstance = true });

        var model = new EditCatalogEntryViewModel
        {
            DisplayName = S["New Custom Chat Instance"],
            Editor = await _sessionDisplayManager.UpdateEditorAsync(session, _updateModelAccessor.ModelUpdater, isNew: true, groupId: DisplayGroups.AICustomChatSession),

        };

        if (ModelState.IsValid)
        {
            await _sessionManager.SaveAsync(session);

            await _notifier.SuccessAsync(H["Custom chat instance has been created successfully."]);

            return RedirectToAction(nameof(Index));
        }

        return View(model);
    }

    [Admin("ai/custom-chat/edit/{sessionId}")]
    public async Task<IActionResult> Edit(string sessionId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageCustomChatInstances))
        {
            return Forbid();
        }

        var session = await _sessionManager.FindAsync(sessionId);

        if (session == null)
        {
            return NotFound();
        }

        var userId = CurrentUserId();

        if (session.UserId != userId)
        {
            return Forbid();
        }

        var metadata = session.As<AIChatInstanceMetadata>();

        if (metadata?.IsCustomInstance != true)
        {
            return NotFound();
        }

        var model = new EditCatalogEntryViewModel
        {
            DisplayName = session.Title ?? S["Custom Chat Instance"],
            Editor = await _sessionDisplayManager.BuildEditorAsync(session, _updateModelAccessor.ModelUpdater, isNew: false, groupId: DisplayGroups.AICustomChatSession),
        };

        return View(model);
    }

    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("ai/custom-chat/edit/{sessionId}")]
    public async Task<IActionResult> EditPost(string sessionId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageCustomChatInstances))
        {
            return Forbid();
        }

        var session = await _sessionManager.FindAsync(sessionId);

        if (session == null)
        {
            return NotFound();
        }

        var userId = CurrentUserId();

        if (session.UserId != userId)
        {
            return Forbid();
        }

        var metadata = session.As<AIChatInstanceMetadata>();

        if (metadata?.IsCustomInstance != true)
        {
            return NotFound();
        }

        var model = new EditCatalogEntryViewModel
        {
            DisplayName = session.Title ?? S["Custom Chat Instance"],
            Editor = await _sessionDisplayManager.UpdateEditorAsync(session, _updateModelAccessor.ModelUpdater, isNew: false, groupId: DisplayGroups.AICustomChatSession),
        };

        if (ModelState.IsValid)
        {
            await _sessionManager.SaveAsync(session);

            await _notifier.SuccessAsync(H["Custom chat instance has been updated successfully."]);

            return RedirectToAction(nameof(Index));
        }

        return View(model);
    }

    [HttpPost]
    [Admin("ai/custom-chat/delete/{sessionId}", "CustomChatDelete")]
    public async Task<IActionResult> Delete(string sessionId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageCustomChatInstances))
        {
            return Forbid();
        }

        var session = await _sessionManager.FindAsync(sessionId);

        if (session == null)
        {
            return NotFound();
        }

        var userId = CurrentUserId();

        if (session.UserId != userId)
        {
            return Forbid();
        }

        var metadata = session.As<AIChatInstanceMetadata>();

        if (metadata?.IsCustomInstance != true)
        {
            return NotFound();
        }

        if (await _sessionManager.DeleteAsync(sessionId))
        {
            await _notifier.SuccessAsync(H["Custom chat instance has been deleted successfully."]);
        }
        else
        {
            await _notifier.ErrorAsync(H["Unable to delete the custom chat instance."]);
        }

        return RedirectToAction(nameof(Index));
    }

    private string CurrentUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier);
}
