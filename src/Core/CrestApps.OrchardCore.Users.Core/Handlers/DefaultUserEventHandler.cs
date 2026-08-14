using OrchardCore.Environment.Cache;
using OrchardCore.Users;
using OrchardCore.Users.Handlers;

namespace CrestApps.OrchardCore.Users.Core.Handlers;

/// <summary>
/// Handles events for default user event.
/// </summary>
public sealed class DefaultUserEventHandler : UserEventHandlerBase
{
    private readonly ITagCache _tagCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultUserEventHandler"/> class.
    /// </summary>
    /// <param name="tagCache">The tag cache.</param>
    public DefaultUserEventHandler(ITagCache tagCache)
    {
        _tagCache = tagCache;
    }

    public override Task CreatedAsync(UserCreateContext context)
        => RemoveTagAsync(context.User);

    public override Task DeletedAsync(UserDeleteContext context)
        => RemoveTagAsync(context.User);

    public override Task DisabledAsync(UserContext context)
        => RemoveTagAsync(context.User);

    public override Task EnabledAsync(UserContext context)
        => RemoveTagAsync(context.User);

    public override Task UpdatedAsync(UserUpdateContext context)
        => RemoveTagAsync(context.User);

    private Task RemoveTagAsync(IUser user)
        => _tagCache.RemoveTagAsync($"{UsersConstants.UserDisplayNameCacheTag}:{user.UserName}");
}
