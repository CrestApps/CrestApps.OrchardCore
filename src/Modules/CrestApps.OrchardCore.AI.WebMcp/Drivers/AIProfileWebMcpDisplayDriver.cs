using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.WebMcp.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.WebMcp.Drivers;

/// <summary>
/// Adds the "Allow page tools" and "Allow consequential page tools" switches to the AI profile editor. They are
/// stored as the profile's <see cref="WebMcpProfileSettings"/>, which CrestApps.Core reads when it offers the page's
/// WebMCP tools to the model.
/// </summary>
public sealed class AIProfileWebMcpDisplayDriver : DisplayDriver<AIProfile>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfileWebMcpDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIProfileWebMcpDisplayDriver(IStringLocalizer<AIProfileWebMcpDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIProfile profile, BuildEditorContext context)
    {
        return Initialize<EditProfileWebMcpViewModel>("AIProfileWebMcp_Edit", model =>
        {
            if (profile.TryGetSettings<WebMcpProfileSettings>(out var settings))
            {
                model.AllowPageTools = settings.AllowPageTools;
                model.AllowConsequentialPageTools = settings.AllowPageTools && settings.AllowConsequentialPageTools;
            }
        }).Location(l => l.Zone("Content", "3").Tab(S["Capabilities"], "9").Card(S["WebMCP"], "40"));
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfile profile, UpdateEditorContext context)
    {
        var model = new EditProfileWebMcpViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        profile.AlterSettings<WebMcpProfileSettings>(settings =>
        {
            settings.AllowPageTools = model.AllowPageTools;

            // Consequential tools are a second opt-in on top of page tools.
            settings.AllowConsequentialPageTools = model.AllowPageTools && model.AllowConsequentialPageTools;
        });

        return Edit(profile, context);
    }
}
