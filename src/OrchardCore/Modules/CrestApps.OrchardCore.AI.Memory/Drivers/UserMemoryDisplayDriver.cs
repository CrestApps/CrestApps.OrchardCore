using System.Security.Claims;
using CrestApps.OrchardCore.AI.Memory.ViewModels;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.AI.Memory.Drivers;

internal sealed class UserMemoryDisplayDriver : DisplayDriver<User>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ICatalogManager<AIMemoryEntry> _memoryManager;
    private readonly IAIMemoryStore _memoryStore;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public UserMemoryDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        ICatalogManager<AIMemoryEntry> memoryManager,
        IAIMemoryStore memoryStore,
        INotifier notifier,
        IHtmlLocalizer<UserMemoryDisplayDriver> htmlLocalizer,
        IStringLocalizer<UserMemoryDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _memoryManager = memoryManager;
        _memoryStore = memoryStore;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
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
        }).Location("Content:10");
    }

    public override async Task<IDisplayResult> UpdateAsync(User user, UpdateEditorContext context)
    {
        if (!IsCurrentUser(user))
        {
            return null;
        }

        var model = new EditUserMemoryViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (!model.ClearMemories)
        {
            return Edit(user, context);
        }

        if (!model.ConfirmClearMemories)
        {
            context.Updater.ModelState.AddModelError($"{Prefix}.{nameof(model.ConfirmClearMemories)}", S["Please confirm that you want to permanently clear all saved AI memory."]);
            return Edit(user, context);
        }

        var memories = await _memoryStore.GetByUserAsync(user.UserId, 0);

        if (memories.Count == 0)
        {
            await _notifier.WarningAsync(H["No saved AI memory was found for your account."]);
            return Edit(user, context);
        }

        foreach (var memory in memories)
        {
            await _memoryManager.DeleteAsync(memory);
        }

        await _memoryStore.SaveChangesAsync();
        await _notifier.SuccessAsync(H["All saved AI memory for your account has been cleared."]);

        return Edit(user, context);
    }

    private bool IsCurrentUser(User user)
    {
        var currentUserId = _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);

        return !string.IsNullOrEmpty(currentUserId) &&
            !string.IsNullOrEmpty(user?.UserId) &&
            string.Equals(currentUserId, user.UserId, StringComparison.Ordinal);
    }
}
