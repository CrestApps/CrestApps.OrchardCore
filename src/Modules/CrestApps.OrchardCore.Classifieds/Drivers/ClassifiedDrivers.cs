using System.Globalization;
using CrestApps.OrchardCore.Classifieds.Models;
using CrestApps.OrchardCore.Classifieds.Services;
using CrestApps.OrchardCore.Classifieds.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.Admin;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentTypes.Editors;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;
using OrchardCore.Mvc.ModelBinding;
using UpdatePartEditorContext = OrchardCore.ContentManagement.Display.Models.UpdatePartEditorContext;

namespace CrestApps.OrchardCore.Classifieds.Drivers;

/// <summary>
/// The Classified part: where a listing is, on the form visitors post it with, and its status and dates for staff;
/// and on the listing itself, its place and whether it's featured.
/// </summary>
internal sealed class ClassifiedPartDisplayDriver : ContentPartDisplayDriver<ClassifiedPart>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public ClassifiedPartDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IStringLocalizer<ClassifiedPartDisplayDriver> stringLocalizer,
        IClock clock)
    {
        _httpContextAccessor = httpContextAccessor;
        _clock = clock;
        S = stringLocalizer;
    }

    public override IDisplayResult Display(ClassifiedPart part, BuildPartDisplayContext context)
        => Initialize<ClassifiedPartViewModel>(GetDisplayShapeType(context), model => Fill(model, part))
            .Location("Detail", "Content:1")
            .Location("Summary", "Meta:5");

    public override IDisplayResult Edit(ClassifiedPart part, BuildPartEditorContext context)
        => Initialize<ClassifiedPartViewModel>(GetEditorShapeType(context), model =>
        {
            Fill(model, part);
            model.IsAdmin = IsAdmin();
        });

    public override async Task<IDisplayResult> UpdateAsync(ClassifiedPart part, UpdatePartEditorContext context)
    {
        var model = new ClassifiedPartViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        part.Place = string.IsNullOrWhiteSpace(model.Place) ? null : model.Place.Trim()[..Math.Min(model.Place.Trim().Length, 200)];

        var hasLatitude = TryParse(model.Latitude, out var latitude);
        var hasLongitude = TryParse(model.Longitude, out var longitude);

        if (hasLatitude != hasLongitude || (hasLatitude && !GeoDistance.IsValid(latitude, longitude)))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Latitude), S["Enter both a latitude (-90 to 90) and a longitude (-180 to 180), such as 47.6062 and -122.3321, or neither."]);
        }
        else
        {
            part.Latitude = hasLatitude ? latitude : null;
            part.Longitude = hasLongitude ? longitude : null;
        }

        // Only staff set a listing's status and dates; a poster's own form leaves them as the moderation made them.
        if (IsAdmin())
        {
            part.Status = Enum.IsDefined(model.Status) ? model.Status : part.Status;
            part.ExpiresUtc = model.ExpiresUtc is { } expires ? DateTime.SpecifyKind(expires, DateTimeKind.Utc) : null;
            part.FeaturedUntilUtc = model.FeaturedUntilUtc is { } featured ? DateTime.SpecifyKind(featured, DateTimeKind.Utc) : null;
        }

        return Edit(part, context);
    }

    private void Fill(ClassifiedPartViewModel model, ClassifiedPart part)
    {
        model.Status = part.Status;
        model.Place = part.Place;
        model.Latitude = part.Latitude?.ToString("0.######", CultureInfo.InvariantCulture);
        model.Longitude = part.Longitude?.ToString("0.######", CultureInfo.InvariantCulture);
        model.ExpiresUtc = part.ExpiresUtc;
        model.FeaturedUntilUtc = part.FeaturedUntilUtc;
        model.RejectReason = part.RejectReason;
        model.IsFeatured = part.FeaturedUntilUtc > _clock.UtcNow;
    }

    private bool IsAdmin()
        => _httpContextAccessor.HttpContext is { } httpContext && AdminAttribute.IsApplied(httpContext);

    private static bool TryParse(string value, out double number)
    {
        number = 0d;

        return !string.IsNullOrWhiteSpace(value) && double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out number);
    }
}

/// <summary>
/// The Classified part's settings on a content type: how long a listing runs, and whether a moderator approves it.
/// </summary>
internal sealed class ClassifiedPartSettingsDisplayDriver : ContentTypePartDefinitionDisplayDriver<ClassifiedPart>
{
    public override IDisplayResult Edit(ContentTypePartDefinition contentTypePartDefinition, BuildEditorContext context)
        => Initialize<ClassifiedPartSettingsViewModel>("ClassifiedPartSettings_Edit", model =>
        {
            var settings = contentTypePartDefinition.GetSettings<ClassifiedPartSettings>();

            model.TermDays = settings.TermDays;
            model.RequireApproval = settings.RequireApproval;
        }).Location("Content");

    public override async Task<IDisplayResult> UpdateAsync(ContentTypePartDefinition contentTypePartDefinition, UpdateTypePartEditorContext context)
    {
        var model = new ClassifiedPartSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        context.Builder.WithSettings(new ClassifiedPartSettings
        {
            TermDays = Math.Clamp(model.TermDays, 1, 3650),
            RequireApproval = model.RequireApproval,
        });

        return Edit(contentTypePartDefinition, context);
    }
}

/// <summary>
/// The Listing upgrade part's editor: what buying it does to a listing.
/// </summary>
internal sealed class ClassifiedUpgradePartDisplayDriver : ContentPartDisplayDriver<ClassifiedUpgradePart>
{
    public override IDisplayResult Edit(ClassifiedUpgradePart part, BuildPartEditorContext context)
        => Initialize<ClassifiedUpgradePartViewModel>(GetEditorShapeType(context), model =>
        {
            model.Kind = part.Kind;
            model.Days = part.Days;
        });

    public override async Task<IDisplayResult> UpdateAsync(ClassifiedUpgradePart part, UpdatePartEditorContext context)
    {
        var model = new ClassifiedUpgradePartViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        part.Kind = Enum.IsDefined(model.Kind) ? model.Kind : ClassifiedUpgradeKind.Feature;
        part.Days = Math.Clamp(model.Days, 1, 365);

        return Edit(part, context);
    }
}

/// <summary>
/// Renders a posted listing as a row of the moderation list, in both of its layouts (List and Grid).
/// </summary>
internal sealed class ClassifiedEntryDisplayDriver : DisplayDriver<ClassifiedEntry>
{
    public override IDisplayResult Display(ClassifiedEntry entry, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return null;
        }

        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = entry.Title?.ToLowerInvariant() ?? string.Empty;
        }

        return Combine(
            View("ClassifiedEntry_Fields_SummaryAdmin", entry)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("ClassifiedEntry_Description_SummaryAdmin", entry)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Details:1"),
            View("ClassifiedEntry_Buttons_SummaryAdmin", entry).Location("Actions:5"));
    }
}
