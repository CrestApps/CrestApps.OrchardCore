using System.Security.Claims;
using CrestApps.OrchardCore.AI.Chat.Models;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.Chat.Controllers;

[Admin]
public sealed class CustomChatController : Controller
{
    private readonly IAIChatSessionManager _sessionManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IDisplayManager<AIChatSession> _sessionDisplayManager;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public CustomChatController(
        IAIChatSessionManager sessionManager,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<AIChatSession> sessionDisplayManager,
        INotifier notifier,
        IHtmlLocalizer<CustomChatController> htmlLocalizer,
        IStringLocalizer<CustomChatController> stringLocalizer
        )
    {
        _sessionManager = sessionManager;
        _authorizationService = authorizationService;
        _updateModelAccessor = updateModelAccessor;
        _sessionDisplayManager = sessionDisplayManager;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    [Admin("ai/custom-chat", "CustomChatIndex")]
    public async Task<IActionResult> Index()
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageCustomChatInstances))
        {
            return Forbid();
        }

        var userId = CurrentUserId();

        // Get all custom chat instances for the current user
        var sessions = await _sessionManager.PageAsync(1, 100, new AIChatSessionQueryContext
        {
            UserId = userId,
        });

        // Filter only custom instances
        var customInstances = sessions.Sessions
            .Where(s => s.As<AIChatInstanceMetadata>()?.IsCustomInstance == true)
            .ToList();

        var viewModel = new ListCatalogEntryViewModel<AIChatSession>
        {
            Models = [],
        };

        foreach (var session in customInstances)
        {
            viewModel.Models.Add(new CatalogEntryViewModel<AIChatSession>
            {
                Model = session,
                Shape = await _sessionDisplayManager.BuildDisplayAsync(session, _updateModelAccessor.ModelUpdater, "SummaryAdmin")
            });
        }

        return View(viewModel);
    }

    [Admin("ai/custom-chat/create", "CustomChatCreate")]
    public async Task<IActionResult> Create()
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageCustomChatInstances))
        {
            return Forbid();
        }

        var userId = CurrentUserId();

        var session = new AIChatSession
        {
            SessionId = Guid.NewGuid().ToString("N"),
            UserId = userId,
            CreatedUtc = DateTime.UtcNow,
            ProfileId = "custom-" + Guid.NewGuid().ToString("N") // Placeholder profile ID
        };

        // Mark as custom instance
        session.Put(new AIChatInstanceMetadata { IsCustomInstance = true });

        var model = new EditCatalogEntryViewModel
        {
            DisplayName = S["New Custom Chat Instance"],
            Editor = await _sessionDisplayManager.BuildEditorAsync(session, _updateModelAccessor.ModelUpdater, isNew: true),
        };

        return View(model);
    }

    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("ai/custom-chat/create", "CustomChatCreate")]
    public async Task<IActionResult> CreatePost()
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageCustomChatInstances))
        {
            return Forbid();
        }

        var userId = CurrentUserId();

        var session = new AIChatSession
        {
            SessionId = Guid.NewGuid().ToString("N"),
            UserId = userId,
            CreatedUtc = DateTime.UtcNow,
            ProfileId = "custom-" + Guid.NewGuid().ToString("N")
        };

        // Mark as custom instance
        session.Put(new AIChatInstanceMetadata { IsCustomInstance = true });

        var model = new EditCatalogEntryViewModel
        {
            DisplayName = S["New Custom Chat Instance"],
            Editor = await _sessionDisplayManager.UpdateEditorAsync(session, _updateModelAccessor.ModelUpdater, isNew: true),
        };

        if (ModelState.IsValid)
        {
            await _sessionManager.SaveAsync(session);

            await _notifier.SuccessAsync(H["Custom chat instance has been created successfully."]);

            return RedirectToAction(nameof(Index));
        }

        return View(model);
    }

    [Admin("ai/custom-chat/edit/{sessionId}", "CustomChatEdit")]
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
            Editor = await _sessionDisplayManager.BuildEditorAsync(session, _updateModelAccessor.ModelUpdater, isNew: false),
        };

        return View(model);
    }

    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("ai/custom-chat/edit/{sessionId}", "CustomChatEdit")]
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
            Editor = await _sessionDisplayManager.UpdateEditorAsync(session, _updateModelAccessor.ModelUpdater, isNew: false),
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
