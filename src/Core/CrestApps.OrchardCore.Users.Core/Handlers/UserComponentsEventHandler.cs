using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Users;
using OrchardCore.Users.Handlers;

namespace CrestApps.OrchardCore.Users.Core.Handlers;

public class UserComponentsEventHandler : IUserEventHandler
{
    private readonly IServiceProvider _serviceProvider;

    private IUserCacheService _userCacheService;

    public UserComponentsEventHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
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
    {
        _userCacheService ??= _serviceProvider.GetRequiredService<IUserCacheService>();

        return _userCacheService.RemoveAsync(user.UserName);
    }
}
