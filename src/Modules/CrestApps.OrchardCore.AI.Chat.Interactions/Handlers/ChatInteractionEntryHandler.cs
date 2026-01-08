using System.Security.Claims;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Core.Handlers;
using CrestApps.OrchardCore.Models;
using Microsoft.AspNetCore.Http;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Handlers;

internal sealed class ChatInteractionEntryHandler : CatalogEntryHandlerBase<ChatInteraction>
{
    private readonly IClock _clock;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ChatInteractionEntryHandler(
        IClock clock,
        IHttpContextAccessor httpContextAccessor)
    {
        _clock = clock;
        _httpContextAccessor = httpContextAccessor;
    }

    public override Task InitializedAsync(InitializedContext<ChatInteraction> context)
    {
        context.Model.CreatedUtc = _clock.UtcNow;

        var user = _httpContextAccessor.HttpContext?.User;

        if (user != null)
        {
            context.Model.OwnerId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            context.Model.Author = user.Identity.Name;
        }

        return Task.CompletedTask;
    }
}
