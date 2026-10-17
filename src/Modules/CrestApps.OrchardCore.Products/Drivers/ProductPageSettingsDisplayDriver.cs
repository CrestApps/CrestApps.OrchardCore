using CrestApps.OrchardCore.Products.Core.Listing;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.ViewModels;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentTypes.Editors;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Products.Drivers;

public sealed class ProductPageSettingsDisplayDriver : ContentTypePartDefinitionDisplayDriver<ProductPart>
{
    public override IDisplayResult Edit(ContentTypePartDefinition contentTypePartDefinition, BuildEditorContext context)
    {
        return Initialize<ProductPageSettingsViewModel>("ProductPageSettings_Edit", model =>
        {
            var settings = contentTypePartDefinition.GetSettings<ProductPageSettings>();

            model.ShowDetails = settings.ShowDetails;
            model.InquiryLabel = settings.InquiryLabel;
            model.InquiryUrl = settings.InquiryUrl;
        }).Location("Content:10");
    }

    public override async Task<IDisplayResult> UpdateAsync(ContentTypePartDefinition contentTypePartDefinition, UpdateTypePartEditorContext context)
    {
        var model = new ProductPageSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        context.Builder.WithSettings(new ProductPageSettings
        {
            ShowDetails = model.ShowDetails,
            InquiryLabel = model.InquiryLabel?.Trim(),
            InquiryUrl = model.InquiryUrl?.Trim(),
        });

        return Edit(contentTypePartDefinition, context);
    }
}
