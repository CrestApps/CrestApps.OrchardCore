using System.Security.Claims;
using CrestApps.Core.AI.Memory;
using CrestApps.OrchardCore.AI.Memory.ViewModels;
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

    /// <summary>
    /// Initializes a new instance of the <see cref="UserMemoryDisplayDriver"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The http context accessor.</param>
    /// <param name="memoryStore">The memory store.</param>
    public UserMemoryDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAIMemoryStore memoryStore)
    {
        _httpContextAccessor = httpContextAccessor;
        _memoryStore = memoryStore;
    }

    public override IDisplayResult Edit(User user, BuildEditorContext context)
    {
        if (!IsCurrentUser(user))
        {
            return null;
        }

        return Initialize<EditUserMemoryViewModel>("UserMemory_Edit", async model =>
        {
            model.MemoryCount = await _memoryStore.CountByUserAsync(user.UserId);
            model.UserId = user.UserId;
            model.ReturnUrl = _httpContextAccessor.HttpContext?.Request.GetEncodedPathAndQuery();
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
