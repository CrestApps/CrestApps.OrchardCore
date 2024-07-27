using CrestApps.OrchardCore.Users.Models;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.Users.Filters;

public class AvatarStylesFilter : IAsyncResultFilter
{
    private readonly IResourceManager _resourceManager;
    private readonly UserAvatarOptions _avatarOptions;

    public AvatarStylesFilter(
        IResourceManager resourceManager,
        IOptions<UserAvatarOptions> options)
    {
        _resourceManager = resourceManager;
        _avatarOptions = options.Value;
    }

    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (_avatarOptions.UseDefaultStyle &&
            context.HttpContext.User.Identity.IsAuthenticated &&
            context.IsViewOrPageResult())
        {
            _resourceManager.RegisterResource("stylesheet", "user-profile-avatar").AtHead();
        }

        await next();
    }
}
