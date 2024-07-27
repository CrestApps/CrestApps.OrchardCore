using OrchardCore.Environment.Cache;
using OrchardCore.Users;
using OrchardCore.Users.Handlers;

namespace CrestApps.OrchardCore.Users.Core.Handlers;

public class DefaultUserEventHandler : IUserEventHandler
{
    private readonly ITagCache _tagCache;

    public DefaultUserEventHandler(ITagCache tagCache)
    {
        _tagCache = tagCache;
    }

    public Task CreatedAsync(UserCreateContext context)
        => RemoveTagAsync(context.User);

    public Task CreatingAsync(UserCreateContext context)
        => Task.CompletedTask;

    public Task DeletedAsync(UserDeleteContext context)
        => RemoveTagAsync(context.User);

    public Task DeletingAsync(UserDeleteContext context)
        => Task.CompletedTask;

    public Task DisabledAsync(UserContext context)
        => RemoveTagAsync(context.User);

    public Task EnabledAsync(UserContext context)
        => RemoveTagAsync(context.User);

    public Task UpdatedAsync(UserUpdateContext context)
        => RemoveTagAsync(context.User);

    public Task UpdatingAsync(UserUpdateContext context)
        => Task.CompletedTask;

    private Task RemoveTagAsync(IUser user)
        => _tagCache.RemoveTagAsync($"username:{user.UserName}");
}
