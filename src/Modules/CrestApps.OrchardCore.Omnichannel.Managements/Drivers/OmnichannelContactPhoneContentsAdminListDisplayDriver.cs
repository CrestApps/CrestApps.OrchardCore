using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using Microsoft.AspNetCore.Http;
using OrchardCore.Contents.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

/// <summary>
/// Surfaces a "Phone" card in the content admin list filters, alongside the built-in Display Text, Type, Stereotype,
/// Status, and Sort cards, so agents can discover the <c>phone:</c> search term (and its exact, begins-with, and
/// ends-with variants). The card is only rendered when the list is scoped exclusively to omnichannel contact content
/// types, keeping unrelated content lists uncluttered.
/// </summary>
internal sealed class OmnichannelContactPhoneContentsAdminListDisplayDriver : DisplayDriver<ContentOptionsViewModel>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly OmnichannelContentTypeProvider _contentTypeProvider;

    public OmnichannelContactPhoneContentsAdminListDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        OmnichannelContentTypeProvider contentTypeProvider)
    {
        _httpContextAccessor = httpContextAccessor;
        _contentTypeProvider = contentTypeProvider;
    }

    public override async Task<IDisplayResult> DisplayAsync(ContentOptionsViewModel model, BuildDisplayContext context)
    {
        if (!await OmnichannelContactListScope.IsContactOnlyListAsync(_httpContextAccessor.HttpContext, _contentTypeProvider))
        {
            return null;
        }

        return View("ContentsAdminFilters_Thumbnail__Phone", model)
            .Location("Thumbnail", "Content:60");
    }
}
