using CrestApps.Core.AI.Chat.Security;
using CrestApps.Core.AI.Security;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Chat;

/// <summary>
/// The chat visitor's address comes from CrestApps.Core's resolver, which reads the connection's address and never the
/// raw <c>X-Forwarded-For</c> header (CrestApps.Core #322, covered by its own tests). Over long polling the chat hub gives
/// that resolver the connection's own request (CrestApps.Core #324). This keeps the two chat features handing it out.
/// </summary>
public sealed class AIVisitorIdentityResolverRegistrationTests
{
    /// <summary>
    /// The two chat features do not depend on each other, so either startup may run last. Whichever does, the container
    /// must hand out the package's resolver, never the null resolver the stores fall back to.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TheContainer_ResolvesThePackageResolver_WithBothChatFeaturesOn(bool chatFirst)
    {
        var services = new ServiceCollection();
        Action<IServiceCollection> chat = s => new CrestApps.OrchardCore.AI.Chat.Startup().ConfigureServices(s);
        Action<IServiceCollection> interactions = s => new CrestApps.OrchardCore.AI.Chat.Interactions.Startup().ConfigureServices(s);

        (chatFirst ? chat : interactions)(services);
        (chatFirst ? interactions : chat)(services);

        var registered = services.Last(descriptor => descriptor.ServiceType == typeof(IAIVisitorIdentityResolver));

        Assert.Equal(typeof(DefaultAIVisitorIdentityResolver), registered.ImplementationType);
    }
}
