using System.Security.Claims;
using CrestApps.Core.AI.Memory;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Memory.Controllers;

/// <summary>
/// Provides endpoints for managing user AI memory.
/// </summary>
[Authorize]
[Feature(MemoryConstants.Feature.Memory)]
public sealed class UserMemoryController : Controller
{
    private readonly IAIMemoryStore _memoryStore;
    private readonly ICatalogManager<AIMemoryEntry> _memoryManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;
    private readonly AdminOptions _adminOptions;

    internal readonly IHtmlLocalizer H;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserMemoryController"/> class.
    /// </summary>
    /// <param name="memoryStore">The memory store.</param>
    /// <param name="memoryManager">The memory manager.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="adminOptions">The admin options.</param>
    public UserMemoryController(
        IAIMemoryStore memoryStore,
        ICatalogManager<AIMemoryEntry> memoryManager,
        IAuthorizationService authorizationService,
        INotifier notifier,
        IHtmlLocalizer<UserMemoryController> htmlLocalizer,
        IOptions<AdminOptions> adminOptions)
    {
        _memoryStore = memoryStore;
        _memoryManager = memoryManager;
        _authorizationService = authorizationService;
        _notifier = notifier;
        _adminOptions = adminOptions.Value;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Clears all saved AI memory for the specified user after confirmation.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="returnUrl">The local return URL.</param>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Admin("ai/memory/user/{userId}/clear", "AIUserMemoryClear")]
    public async Task<IActionResult> Clear(string userId, string returnUrl = null)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ClearAIMemory, (object)userId))
        {
            return Forbid();
        }

        var isOtherUser = !IsCurrentUser(userId);
        var safeReturnUrl = GetSafeReturnUrl(returnUrl);
        var memoryCount = await _memoryStore.CountByUserAsync(userId);

        if (memoryCount == 0)
        {
            await _notifier.WarningAsync(
                isOtherUser
                    ? H["No saved AI memory was found for this user."]
                    : H["No saved AI memory was found for your account."]);

            return Redirect(safeReturnUrl);
        }

        var memories = (await _memoryStore.GetAllAsync())
            .Where(memory => string.Equals(memory.UserId, userId, StringComparison.Ordinal))
            .ToArray();

        if (memories.Length == 0)
        {
            await _notifier.WarningAsync(
                isOtherUser
                    ? H["No saved AI memory was found for this user."]
                    : H["No saved AI memory was found for your account."]);

            return Redirect(safeReturnUrl);
        }

        var removedCount = 0;

        foreach (var memory in memories)
        {
            if (await _memoryManager.DeleteAsync(memory))
            {
                removedCount++;
            }
        }

        if (removedCount == 0)
        {
            await _notifier.ErrorAsync(
                isOtherUser
                    ? H["Unable to remove the saved AI memory for this user."]
                    : H["Unable to remove the saved AI memory for your account."]);
        }
        else if (removedCount == memories.Length)
        {
            await _notifier.SuccessAsync(
                isOtherUser
                    ? H["All saved AI memory for this user has been cleared."]
                    : H["All saved AI memory for your account has been cleared."]);
        }
        else
        {
            await _notifier.WarningAsync(
                isOtherUser
                    ? H["Some saved AI memory entries could not be removed for this user."]
                    : H["Some saved AI memory entries could not be removed from your account."]);
        }

        return Redirect(safeReturnUrl);
    }

    private bool IsCurrentUser(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return !string.IsNullOrWhiteSpace(currentUserId) &&
            string.Equals(currentUserId, userId, StringComparison.Ordinal);
    }

    private string GetSafeReturnUrl(string returnUrl)
    {
        if (Url.IsLocalUrl(returnUrl))
        {
            return returnUrl;
        }

        return Url.Content($"~/{_adminOptions.AdminUrlPrefix}");
    }
}
