# Orchard Core Display Management Examples

## Example 1: Blog Post Subtitle Part Display Driver

A complete display driver for a SubtitlePart:

```csharp
using OrchardCore.ContentManagement;

public sealed class SubtitlePart : ContentPart
{
    public string Subtitle { get; set; }
}
```

```csharp
public class SubtitlePartViewModel
{
    public string Subtitle { get; set; }
    public ContentItem ContentItem { get; set; }
}
```

```csharp
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;

public sealed class SubtitlePartDisplayDriver : ContentPartDisplayDriver<SubtitlePart>
{
    public override IDisplayResult Display(SubtitlePart part, BuildPartDisplayContext context)
    {
        return Initialize<SubtitlePartViewModel>("SubtitlePart", model =>
        {
            model.Subtitle = part.Subtitle;
            model.ContentItem = part.ContentItem;
        })
        .Location("Detail", "Content:3")
        .Location("Summary", "Content:3");
    }

    public override IDisplayResult Edit(SubtitlePart part, BuildPartEditorContext context)
    {
        return Initialize<SubtitlePartViewModel>("SubtitlePart_Edit", model =>
        {
            model.Subtitle = part.Subtitle;
            model.ContentItem = part.ContentItem;
        });
    }

    public override async Task<IDisplayResult> UpdateAsync(SubtitlePart part, UpdatePartEditorContext context)
    {
        var model = new SubtitlePartViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);
        part.Subtitle = model.Subtitle;
        return Edit(part, context);
    }
}
```

### Display Template (Views/SubtitlePart.cshtml)

```cshtml
@model SubtitlePartViewModel

<h2 class="subtitle">@Model.Subtitle</h2>
```

### Editor Template (Views/SubtitlePart_Edit.cshtml)

```cshtml
@model SubtitlePartViewModel

<div class="mb-3">
    <label asp-for="Subtitle" class="form-label">Subtitle</label>
    <input asp-for="Subtitle" class="form-control" />
    <span asp-validation-for="Subtitle" class="text-danger"></span>
</div>
```

### Registration in Startup.cs

```csharp
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContentPart<SubtitlePart>()
            .UseDisplayDriver<SubtitlePartDisplayDriver>();
    }
}
```

## Example 2: Content Part with Multiple Display Types

```csharp
public sealed class ProductInfoPartDisplayDriver : ContentPartDisplayDriver<ProductInfoPart>
{
    public override IDisplayResult Display(ProductInfoPart part, BuildPartDisplayContext context)
    {
        return Combine(
            Initialize<ProductInfoPartViewModel>("ProductInfoPart", model =>
            {
                model.Price = part.Price;
                model.Sku = part.Sku;
            })
            .Location("Detail", "Content:5"),

            Initialize<ProductInfoPartViewModel>("ProductInfoPart_Summary", model =>
            {
                model.Price = part.Price;
            })
            .Location("Summary", "Meta:5"),

            Initialize<ProductInfoPartViewModel>("ProductInfoPart_SummaryAdmin", model =>
            {
                model.Price = part.Price;
                model.Sku = part.Sku;
            })
            .Location("SummaryAdmin", "Meta:5")
        );
    }
}
```
