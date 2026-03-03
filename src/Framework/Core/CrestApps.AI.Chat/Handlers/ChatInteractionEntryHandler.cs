using System.Security.Claims;
using CrestApps.AI.Models;
using CrestApps.Handlers;
using CrestApps.Models;
using Microsoft.AspNetCore.Http;

namespace CrestApps.AI.Chat.Handlers;

internal sealed class ChatInteractionEntryHandler : CatalogEntryHandlerBase<ChatInteraction>
{
    private readonly TimeProvider _timeProvider;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ChatInteractionEntryHandler(
        TimeProvider timeProvider,
        IHttpContextAccessor httpContextAccessor)
    {
        _timeProvider = timeProvider;
        _httpContextAccessor = httpContextAccessor;
    }

    public override Task InitializedAsync(InitializedContext<ChatInteraction> context)
    {
        context.Model.CreatedUtc = _timeProvider.GetUtcNow().UtcDateTime;

        var user = _httpContextAccessor.HttpContext?.User;

        if (user != null)
        {
            context.Model.OwnerId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            context.Model.Author = user.Identity.Name;
        }

        return Task.CompletedTask;
    }
}
