using System.Security.Claims;
using CrestApps.OrchardCore.AI.Core.Indexes;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Services;
using CrestApps.OrchardCore.YesSql.Core.Services;
using Microsoft.AspNetCore.Http;
using YesSql;

namespace CrestApps.OrchardCore.AI.Core.Services;

/// <summary>
/// A specialized catalog for ChatInteraction that enforces user-scoping.
/// Users can only access their own interactions.
/// </summary>
public sealed class ChatInteractionCatalog : SourceDocumentCatalog<ChatInteraction, ChatInteractionIndex>, IChatInteractionCatalog
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ChatInteractionCatalog(
        ISession session,
        IHttpContextAccessor httpContextAccessor)
        : base(session)
    {
        _httpContextAccessor = httpContextAccessor;
        CollectionName = AIConstants.CollectionName;
    }

    public async ValueTask<ChatInteraction> FindByIdForUserAsync(string itemId)
    {
        ArgumentException.ThrowIfNullOrEmpty(itemId);

        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
        {
            return null;
        }

        return await Session.Query<ChatInteraction, ChatInteractionIndex>(
            x => x.ItemId == itemId && x.UserId == userId,
            collection: CollectionName)
            .FirstOrDefaultAsync();
    }

    public async ValueTask<PageResult<ChatInteraction>> PageForUserAsync(int page, int pageSize, ChatInteractionQueryContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
        {
            return new PageResult<ChatInteraction>
            {
                Count = 0,
                Entries = [],
            };
        }

        var query = Session.Query<ChatInteraction, ChatInteractionIndex>(
            i => i.UserId == userId,
            collection: CollectionName);

        if (!string.IsNullOrEmpty(context.Title))
        {
            query = query.Where(i => i.Title.Contains(context.Title));
        }

        var skip = (page - 1) * pageSize;

        return new PageResult<ChatInteraction>
        {
            Count = await query.CountAsync(),
            Entries = (await query.OrderByDescending(i => i.ModifiedUtc)
                .ThenByDescending(x => x.Id)
                .Skip(skip)
                .Take(pageSize)
                .ListAsync()).ToArray()
        };
    }

    public async ValueTask<bool> DeleteForUserAsync(string itemId)
    {
        ArgumentException.ThrowIfNullOrEmpty(itemId);

        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
        {
            return false;
        }

        var interaction = await Session.Query<ChatInteraction, ChatInteractionIndex>(
            i => i.ItemId == itemId && i.UserId == userId,
            collection: CollectionName)
            .FirstOrDefaultAsync();

        if (interaction == null)
        {
            return false;
        }

        Session.Delete(interaction, CollectionName);

        return true;
    }

    public async ValueTask<int> DeleteAllForUserAsync()
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
        {
            return 0;
        }

        var interactions = await Session.Query<ChatInteraction, ChatInteractionIndex>(
            i => i.UserId == userId,
            collection: CollectionName)
            .ListAsync();

        var totalDeleted = 0;

        foreach (var interaction in interactions)
        {
            Session.Delete(interaction, CollectionName);
            totalDeleted++;
        }

        return totalDeleted;
    }

    private string GetCurrentUserId()
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (user?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        return user.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}
