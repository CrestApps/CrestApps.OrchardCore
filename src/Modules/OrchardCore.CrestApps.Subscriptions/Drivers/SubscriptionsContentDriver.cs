using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.CrestApps.Subscriptions.ViewModels;
using OrchardCore.DisplayManagement.Views;

namespace OrchardCore.CrestApps.Subscriptions.Drivers;

public class SubscriptionsContentDriver : ContentDisplayDriver
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public SubscriptionsContentDriver(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public override bool CanHandleModel(ContentItem model)
    {
        var contentType = _contentDefinitionManager.GetTypeDefinitionAsync(model.ContentType).GetAwaiter().GetResult();

        return contentType?.StereotypeEquals("Subscriptions") ?? false;
    }

    public override IDisplayResult Edit(ContentItem model)
    {
        return Initialize<SubscriptionPartViewModel>("SubscriptionPart_Edit", viewModel =>
        {

        });
    }
}
