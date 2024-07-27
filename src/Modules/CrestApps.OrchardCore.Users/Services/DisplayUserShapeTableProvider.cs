using CrestApps.OrchardCore.Users.Core.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Descriptors;

namespace CrestApps.OrchardCore.Users.Services;

public sealed class DisplayUserShapeTableProvider : IShapeTableProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public DisplayUserShapeTableProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public ValueTask DiscoverAsync(ShapeTableBuilder builder)
    {
        builder.Describe("UserMenuItems")
               .Placement(context =>
               {
                   if (context.Differentiator.Equals("Title", StringComparison.Ordinal))
                   {
                       var displayUserOptions = _httpContextAccessor.HttpContext.RequestServices
                       .GetRequiredService<IOptions<DisplayUserOptions>>().Value;

                       return displayUserOptions.ConvertAuthorToShape;
                   }

                   return false;
               }
               , new PlacementInfo() { Location = "-" });

        return ValueTask.CompletedTask;
    }
}
