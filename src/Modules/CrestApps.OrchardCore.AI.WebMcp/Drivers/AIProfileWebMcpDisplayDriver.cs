using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.WebMcp.Models;
using CrestApps.OrchardCore.AI.WebMcp.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.WebMcp.Drivers;

/// <summary>
/// Adds the "Allow page tools" switch to the AI profile editor.
/// </summary>
public sealed class AIProfileWebMcpDisplayDriver : DisplayDriver<AIProfile>
{
    public override IDisplayResult Edit(AIProfile profile, BuildEditorContext context)
    {
        return Initialize<EditProfileWebMcpViewModel>("AIProfileWebMcp_Edit", model =>
        {
            if (profile.TryGetSettings<WebMcpProfileSettings>(out var settings))
            {
                model.AllowPageTools = settings.AllowPageTools;
            }
        }).Location("Content:3#Capabilities;9");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfile profile, UpdateEditorContext context)
    {
        var model = new EditProfileWebMcpViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        profile.AlterSettings<WebMcpProfileSettings>(settings => settings.AllowPageTools = model.AllowPageTools);

        return Edit(profile, context);
    }
}
