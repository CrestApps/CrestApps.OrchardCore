using System.Security.Claims;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using Microsoft.AspNetCore.Http;

namespace CrestApps.Core.AI.Chat.Handlers;

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
