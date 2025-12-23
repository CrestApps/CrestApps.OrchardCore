using System.Security.Claims;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Models;
using Microsoft.AspNetCore.Http;
using OrchardCore;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Core.Services;

public sealed class DefaultChatInteractionManager : IChatInteractionManager
{
    private readonly IClock _clock;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IChatInteractionCatalog _catalog;

    public DefaultChatInteractionManager(
        IClock clock,
        IHttpContextAccessor httpContextAccessor,
        IChatInteractionCatalog catalog)
    {
        _clock = clock;
        _httpContextAccessor = httpContextAccessor;
        _catalog = catalog;
    }

    public ValueTask<ChatInteraction> NewAsync(string source)
    {
        ArgumentException.ThrowIfNullOrEmpty(source);

        var user = _httpContextAccessor.HttpContext?.User;

        if (user?.Identity?.IsAuthenticated != true)
        {
            throw new UnauthorizedAccessException("User must be authenticated to create a chat interaction.");
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var now = _clock.UtcNow;

        var interaction = new ChatInteraction
        {
            ItemId = IdGenerator.GenerateId(),
            UserId = userId,
            Source = source,
            CreatedUtc = now,
            ModifiedUtc = now,
        };

        return ValueTask.FromResult(interaction);
    }

    public ValueTask<PageResult<ChatInteraction>> PageAsync(int page, int pageSize, ChatInteractionQueryContext context)
    {
        return _catalog.PageForUserAsync(page, pageSize, context);
    }

    public ValueTask<ChatInteraction> FindAsync(string itemId)
    {
        return _catalog.FindByIdForUserAsync(itemId);
    }

    public async ValueTask CreateAsync(ChatInteraction interaction)
    {
        ArgumentNullException.ThrowIfNull(interaction);

        interaction.ModifiedUtc = _clock.UtcNow;

        await _catalog.CreateAsync(interaction);
        await _catalog.SaveChangesAsync();
    }

    public async ValueTask UpdateAsync(ChatInteraction interaction)
    {
        ArgumentNullException.ThrowIfNull(interaction);

        interaction.ModifiedUtc = _clock.UtcNow;

        await _catalog.UpdateAsync(interaction);
        await _catalog.SaveChangesAsync();
    }

    public async ValueTask<bool> DeleteAsync(string itemId)
    {
        var result = await _catalog.DeleteForUserAsync(itemId);

        if (result)
        {
            await _catalog.SaveChangesAsync();
        }

        return result;
    }

    public async ValueTask<int> DeleteAllAsync()
    {
        var count = await _catalog.DeleteAllForUserAsync();

        if (count > 0)
        {
            await _catalog.SaveChangesAsync();
        }

        return count;
    }
}
