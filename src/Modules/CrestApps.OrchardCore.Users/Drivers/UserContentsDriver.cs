using CrestApps.OrchardCore.Users.Core.Models;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.ViewModels;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Users.Drivers;

public sealed class UserContentsDriver : ContentDisplayDriver
{
    private readonly DisplayUserOptions _displayUserOptions;

    public UserContentsDriver(IOptions<DisplayUserOptions> displayUserOptions)
    {
        _displayUserOptions = displayUserOptions.Value;
    }

    public override IDisplayResult Display(ContentItem contentItem)
    {
        if (!_displayUserOptions.ConvertAuthorToShape)
        {
            return null;
        }

        return Initialize<ContentItemViewModel>("ContentsMeta_SummaryAdmin_User", model =>
        {
            model.ContentItem = contentItem;
        }).Location("SummaryAdmin", "Meta:25");
    }
}
