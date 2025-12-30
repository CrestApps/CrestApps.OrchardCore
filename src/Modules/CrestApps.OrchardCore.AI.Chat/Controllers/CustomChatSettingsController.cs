using System.Security.Claims;
using CrestApps.OrchardCore.AI.Chat.Indexes;
using CrestApps.OrchardCore.AI.Chat.Models;
using CrestApps.OrchardCore.AI.Chat.ViewModels;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.Admin;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using YesSql;

namespace CrestApps.OrchardCore.AI.Chat.Controllers;

[Admin]
public sealed class CustomChatSettingsController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IContentManager _contentManager;
    private readonly ISession _session;
    private readonly IContentItemDisplayManager _contentDisplayManager;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public CustomChatSettingsController(
        IAuthorizationService authorizationService,
        ISession session,
        IContentManager contentManager,
        IContentItemDisplayManager contentItemDisplayManager,
        IUpdateModelAccessor updateModelAccessor,
        INotifier notifier,
        IHtmlLocalizer<CustomChatSettingsController> htmlLocalizer,
        IStringLocalizer<CustomChatSettingsController> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _contentManager = contentManager;
        _contentDisplayManager = contentItemDisplayManager;
        _updateModelAccessor = updateModelAccessor;
        _session = session;
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

        var records = await _session.QueryIndex<CustomChatPartIndex>(x => x.IsCustomInstance && x.UserId == CurrentUserId())
          .OrderByDescending(x => x.CreatedUtc)
          .ListAsync();

        var customChat = new List<ContentItem>();

        foreach (var record in records)
        {
            var widget = await _contentManager.GetAsync(record.ContentItemId);

            if (widget == null)
            {
                continue;
            }

            customChat.Add(widget);
        }

        return View(new ListCatalogEntryViewModel<ContentItem>
        {
            Models = customChat
        });
    }

    [Admin("ai/custom-chat/chat/{contentItemId}")]
    public async Task<IActionResult> Chat(string contentItemId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageCustomChatInstances))
        {
            return Forbid();
        }

        var contentItem = await _contentManager.GetAsync(contentItemId);

        if (contentItem == null)
        {
            return NotFound();
        }

        var part = contentItem.As<CustomChatPart>();

        if (part.UserId != CurrentUserId())
        {
            return Forbid();
        }

        var shape = await _contentDisplayManager.BuildEditorAsync(contentItem, _updateModelAccessor.ModelUpdater, isNew: false);

        return View(new ManageCustomChatInstancesViewModel
        {
            ChatContent = shape
        });
    }


    [Admin("ai/custom-chat/create")]
    public async Task<IActionResult> Create()
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageCustomChatInstances))
        {
            return Forbid();
        }

        var customChatItem = await _contentManager.NewAsync("CustomChat");

        var part = customChatItem.As<CustomChatPart>();
        part.CustomChatInstanceId = IdGenerator.GenerateId();
        part.SessionId = IdGenerator.GenerateId();
        part.UserId = CurrentUserId();
        part.CreatedUtc = DateTime.UtcNow;
        part.IsCustomInstance = true;

        customChatItem.Apply(part);

        var editor = await _contentDisplayManager.BuildEditorAsync(customChatItem, _updateModelAccessor.ModelUpdater, isNew: true);

        return View(new EditCatalogEntryViewModel
        {
            DisplayName = S["New Custom Chat Instance"],
            Editor = editor
        });
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

        var customChatItem = await _contentManager.NewAsync("CustomChat");

        var editor = await _contentDisplayManager.UpdateEditorAsync(customChatItem, _updateModelAccessor.ModelUpdater, isNew: true);

        if (!ModelState.IsValid)
        {
            return View(new EditCatalogEntryViewModel
            {
                DisplayName = S["New Custom Chat Instance"],
                Editor = editor
            });
        }

        var part = customChatItem.As<CustomChatPart>();

        part.UserId = CurrentUserId();
        part.IsCustomInstance = true;
        part.CreatedUtc = DateTime.UtcNow;

        if (string.IsNullOrEmpty(part.CustomChatInstanceId))
        {
            part.CustomChatInstanceId = IdGenerator.GenerateId();
        }

        if (string.IsNullOrEmpty(part.SessionId))
        {
            part.SessionId = IdGenerator.GenerateId();
        }

        customChatItem.Apply(part);

        await _contentManager.CreateAsync(customChatItem);

        await _notifier.SuccessAsync(H["Custom chat instance created successfully."]);

        return RedirectToAction(nameof(Index));
    }


    [Admin("ai/custom-chat/edit/{contentItemId}")]
    public async Task<IActionResult> Edit(string contentItemId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageCustomChatInstances))
        {
            return Forbid();
        }

        var customChatItem = await _contentManager.GetAsync(contentItemId);

        if (customChatItem == null)
        {
            return NotFound();
        }

        var part = customChatItem.As<CustomChatPart>();

        if (part.UserId != CurrentUserId())
        {
            return Forbid();
        }

        var editor = await _contentDisplayManager.BuildEditorAsync(customChatItem, _updateModelAccessor.ModelUpdater, isNew: false);

        return View(new EditCatalogEntryViewModel
        {
            DisplayName = customChatItem.DisplayText,
            Editor = editor
        });
    }

    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("ai/custom-chat/edit/{contentItemId}")]
    public async Task<IActionResult> EditPost(string contentItemId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageCustomChatInstances))
        {
            return Forbid();
        }

        var customChatItem = await _contentManager.GetAsync(contentItemId);

        if (customChatItem == null)
        {
            return NotFound();
        }

        var part = customChatItem.As<CustomChatPart>();

        if (part.UserId != CurrentUserId())
        {
            return Forbid();
        }

        var editor = await _contentDisplayManager.UpdateEditorAsync(customChatItem, _updateModelAccessor.ModelUpdater, isNew: false);

        if (!ModelState.IsValid)
        {
            return View(new EditCatalogEntryViewModel
            {
                DisplayName = customChatItem.DisplayText,
                Editor = editor
            });
        }

        await _contentManager.UpdateAsync(customChatItem);

        await _notifier.SuccessAsync(H["Custom chat instance updated successfully."]);

        return RedirectToAction(nameof(Edit));
    }

    [HttpPost]
    [Admin("ai/custom-chat/delete/{contentItemId}", "CustomChatDelete")]
    public async Task<IActionResult> Delete(string contentItemId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageCustomChatInstances))
        {
            return Forbid();
        }

        var customChatItem = await _contentManager.GetAsync(contentItemId);

        if (customChatItem == null)
        {
            return NotFound();
        }

        var part = customChatItem.As<CustomChatPart>();

        if (part.UserId != CurrentUserId())
        {
            return Forbid();
        }

        await _contentManager.RemoveAsync(customChatItem);

        await _notifier.SuccessAsync(H["Custom chat instance has been deleted successfully."]);

        return RedirectToAction(nameof(Index));
    }

    private string CurrentUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier);
}
