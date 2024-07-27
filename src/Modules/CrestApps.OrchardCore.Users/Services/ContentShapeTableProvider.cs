using CrestApps.OrchardCore.Users.Core.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Descriptors;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Users.Services;

[RequireFeatures("OrchardCore.Contents")]
public sealed class ContentShapeTableProvider : IShapeTableProvider
{
    private const string ContentMetaShapeType = "ContentsMeta_SummaryAdmin";
    private const string SummaryAdmin = "SummaryAdmin";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public ContentShapeTableProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public ValueTask DiscoverAsync(ShapeTableBuilder builder)
    {
        builder.Describe(ContentMetaShapeType)
               .Placement(context =>
               {
                   if (context.DisplayType == SummaryAdmin)
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
