using System.Security.Claims;
using CrestApps.Core.AI.Memory;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Memory.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.AI.Memory.Drivers;

internal sealed class UserMemoryDisplayDriver : DisplayDriver<User>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAIMemoryStore _memoryStore;
    private readonly IAuthorizationService _authorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserMemoryDisplayDriver"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The http context accessor.</param>
    /// <param name="memoryStore">The memory store.</param>
    /// <param name="authorizationService">The authorization service.</param>
    public UserMemoryDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAIMemoryStore memoryStore,
        IAuthorizationService authorizationService)
    {
        _httpContextAccessor = httpContextAccessor;
        _memoryStore = memoryStore;
        _authorizationService = authorizationService;
    }

    public override IDisplayResult Edit(User user, BuildEditorContext context)
    {
        return Initialize<EditUserMemoryViewModel>("UserMemory_Edit", async model =>
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var currentUser = httpContext?.User;

            if (currentUser == null ||
                !await _authorizationService.AuthorizeAsync(currentUser, AIPermissions.ClearAIMemory, (object)user.UserId))
            {
                return;
            }

            var isOtherUser = !IsCurrentUser(user);
            var memoryCount = await _memoryStore.CountByUserAsync(user.UserId);

            model.HasMemories = memoryCount > 0;
            model.IsOtherUser = isOtherUser;
            model.UserId = user.UserId;
            model.ReturnUrl = httpContext?.Request.GetEncodedPathAndQuery();
        }).Location("Content:10");
    }

    private bool IsCurrentUser(User user)
    {
        var currentUserId = _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);

        return !string.IsNullOrEmpty(currentUserId) &&
            !string.IsNullOrEmpty(user?.UserId) &&
                string.Equals(currentUserId, user.UserId, StringComparison.Ordinal);
    }
}
