using System.Security.Claims;
using CrestApps.OrchardCore.AI.Core.Indexes;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.Support;
using Microsoft.AspNetCore.Http;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.AI.Core.Services;

public sealed class DefaultChatInteractionManager : IChatInteractionManager
{
    private readonly IClock _clock;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly YesSql.ISession _session;

    public DefaultChatInteractionManager(
        IClock clock,
        IHttpContextAccessor httpContextAccessor,
        YesSql.ISession session)
    {
        _clock = clock;
        _httpContextAccessor = httpContextAccessor;
        _session = session;
    }

    public Task<ChatInteraction> NewAsync()
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (user?.Identity?.IsAuthenticated != true)
        {
            throw new UnauthorizedAccessException("User must be authenticated to create a chat interaction.");
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var now = _clock.UtcNow;

        var interaction = new ChatInteraction
        {
            InteractionId = IdGenerator.GenerateId(),
            UserId = userId,
            CreatedUtc = now,
            ModifiedUtc = now,
        };

        return Task.FromResult(interaction);
    }

    public async Task<ChatInteractionResult> PageAsync(int page, int pageSize, ChatInteractionQueryContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var user = _httpContextAccessor.HttpContext?.User;

        if (user?.Identity?.IsAuthenticated != true)
        {
            return new ChatInteractionResult
            {
                Count = 0,
                Interactions = [],
            };
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);

        var query = _session.Query<ChatInteraction, ChatInteractionIndex>(
            i => i.UserId == userId,
            collection: AIConstants.CollectionName);

        if (!string.IsNullOrEmpty(context.Title))
        {
            query = query.Where(i => i.Title.Contains(context.Title));
        }

        return new ChatInteractionResult
        {
            Count = await query.CountAsync(),
            Interactions = await query.OrderByDescending(i => i.ModifiedUtc)
                .ThenByDescending(x => x.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ListAsync()
        };
    }

    public async Task<ChatInteraction> FindAsync(string interactionId)
    {
        ArgumentException.ThrowIfNullOrEmpty(interactionId);

        var user = _httpContextAccessor.HttpContext?.User;

        if (user?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);

        return await _session.Query<ChatInteraction, ChatInteractionIndex>(
            i => i.InteractionId == interactionId && i.UserId == userId,
            collection: AIConstants.CollectionName)
            .FirstOrDefaultAsync();
    }

    public Task SaveAsync(ChatInteraction interaction)
    {
        ArgumentNullException.ThrowIfNull(interaction);

        interaction.ModifiedUtc = _clock.UtcNow;

        return _session.SaveAsync(interaction, collection: AIConstants.CollectionName);
    }

    public async Task<bool> DeleteAsync(string interactionId)
    {
        ArgumentException.ThrowIfNullOrEmpty(interactionId);

        var user = _httpContextAccessor.HttpContext?.User;

        if (user?.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);

        var interaction = await _session.Query<ChatInteraction, ChatInteractionIndex>(
            i => i.InteractionId == interactionId && i.UserId == userId,
            collection: AIConstants.CollectionName)
            .FirstOrDefaultAsync();

        if (interaction == null)
        {
            return false;
        }

        _session.Delete(interaction, collection: AIConstants.CollectionName);

        return true;
    }

    public async Task<int> DeleteAllAsync()
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (user?.Identity?.IsAuthenticated != true)
        {
            return 0;
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);

        var interactions = await _session.Query<ChatInteraction, ChatInteractionIndex>(
            i => i.UserId == userId,
            collection: AIConstants.CollectionName)
            .ListAsync();

        var totalDeleted = 0;

        foreach (var interaction in interactions)
        {
            _session.Delete(interaction, collection: AIConstants.CollectionName);
            totalDeleted++;
        }

        return totalDeleted;
    }
}
