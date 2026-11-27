using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentTypes.Editors;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

internal sealed class OmnichannelContactPartSettingsDisplayDriver : ContentTypePartDefinitionDisplayDriver<OmnichannelContactPart>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelContactPartSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public OmnichannelContactPartSettingsDisplayDriver(IStringLocalizer<OmnichannelContactPartSettingsDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ContentTypePartDefinition contentTypePartDefinition, BuildEditorContext context)
    {
        return Initialize<OmnichannelContactPartSettingsViewModel>("OmnichannelContactPartSettings_Edit", model =>
        {
            var settings = contentTypePartDefinition.GetSettings<OmnichannelContactPartSettings>();

            model.RequireTimeZone = settings.RequireTimeZone;
            model.AutoDetectTimeZone = settings.AutoDetectTimeZone;
            model.UseDoNotCall = settings.UseDoNotCall;
            model.UseDoNotSms = settings.UseDoNotSms;
            model.UseDoNotEmail = settings.UseDoNotEmail;
        }).Location(l => l.Zone("Content", "5").Card(S["Settings"], "20"));
    }

    public override async Task<IDisplayResult> UpdateAsync(ContentTypePartDefinition contentTypePartDefinition, UpdateTypePartEditorContext context)
    {
        var model = new OmnichannelContactPartSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        context.Builder.WithSettings(new OmnichannelContactPartSettings
        {
            RequireTimeZone = model.RequireTimeZone,
            AutoDetectTimeZone = model.AutoDetectTimeZone,
            UseDoNotCall = model.UseDoNotCall,
            UseDoNotSms = model.UseDoNotSms,
            UseDoNotEmail = model.UseDoNotEmail,
        });

        return Edit(contentTypePartDefinition, context);
    }
}
