using CrestApps.OrchardCore.Payments.Core.Models;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentTypes.Editors;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Products.Drivers;

public sealed class ProductPartSettingsDisplayDriver : ContentTypePartDefinitionDisplayDriver<ProductPart>
{
    internal readonly IStringLocalizer S;

    public ProductPartSettingsDisplayDriver(IStringLocalizer<ProductPartSettingsDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ContentTypePartDefinition contentTypePartDefinition, BuildEditorContext context)
    {
        return Initialize<ProductPartSettingsViewModel>("ProductPartSettings_Edit", model =>
        {
            var settings = contentTypePartDefinition.GetSettings<ProductPartSettings>();

            model.Type = settings.Type;
            model.Types =
            [
                new SelectListItem(S["Undefined"], nameof(ProductType.Undefined)),
                new SelectListItem(S["Good"], nameof(ProductType.Good)),
                new SelectListItem(S["Service"], nameof(ProductType.Service)),
                new SelectListItem(S["Planet"], nameof(ProductType.Planet)),
            ];
        }).Location("Content");
    }

    public override async Task<IDisplayResult> UpdateAsync(ContentTypePartDefinition contentTypePartDefinition, UpdateTypePartEditorContext context)
    {
        var model = new ProductPartSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        context.Builder.WithSettings(new ProductPartSettings()
        {
            Type = model.Type,
        });

        return Edit(contentTypePartDefinition, context);
    }
}
