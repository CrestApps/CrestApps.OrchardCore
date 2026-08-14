using CrestApps.Core.AI.Models;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.AI.Chat.Handlers;
using CrestApps.OrchardCore.AI.Chat.Services;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Chat;

public sealed class AIProfileAdminMenuCacheHandlerTests
{
    [Fact]
    public async Task ProfileChanges_InvalidateAdminMenuCache()
    {
        var cacheService = new Mock<IAIProfileAdminMenuCacheService>();
        var handler = new AIProfileAdminMenuCacheHandler(cacheService.Object);
        var profile = new AIProfile { ItemId = "chat-profile", Name = "chat-profile", Type = AIProfileType.Chat };

        await handler.CreatedAsync(new CreatedContext<AIProfile>(profile), TestContext.Current.CancellationToken);
        await handler.UpdatedAsync(new UpdatedContext<AIProfile>(profile), TestContext.Current.CancellationToken);
        await handler.DeletedAsync(new DeletedContext<AIProfile>(profile), TestContext.Current.CancellationToken);

        cacheService.Verify(x => x.InvalidateAsync(It.IsAny<CancellationToken>()), Times.Exactly(3));
    }
}
