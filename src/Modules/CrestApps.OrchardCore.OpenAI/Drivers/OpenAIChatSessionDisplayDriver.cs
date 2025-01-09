using CrestApps.OrchardCore.OpenAI.Models;
using CrestApps.OrchardCore.OpenAI.ViewModels.Sessions;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.OpenAI.Drivers;

public sealed class OpenAIChatSessionDisplayDriver : DisplayDriver<OpenAIChatSession>
{
    private readonly IOpenAIChatProfileManager _openAIChatProfileManager;

    public OpenAIChatSessionDisplayDriver(IOpenAIChatProfileManager openAIChatProfileManager)
    {
        _openAIChatProfileManager = openAIChatProfileManager;
    }

    public override IDisplayResult Display(OpenAIChatSession session, BuildDisplayContext context)
    {
        return Initialize<DisplayAIChatSessionViewModel>("OpenAIChatSessionListItem", model =>
        {
            model.Session = session;
        }).Location("SummaryAdmin", "Content");
    }

    public override async Task<IDisplayResult> EditAsync(OpenAIChatSession session, BuildEditorContext context)
    {
        var profile = await _openAIChatProfileManager.FindByIdAsync(session.ProfileId);

        if (profile == null)
        {
            return null;
        }

        var headerResult = Initialize<ChatSessionCapsuleViewModel>("OpenAIChatSessionHeader", model =>
        {
            model.Session = session;
            model.Profile = profile;
            model.IsNew = context.IsNew;
        }).Location("Header");

        var contentResult = Initialize<ChatSessionCapsuleViewModel>("OpenAIChatSessionChat", model =>
        {
            model.Session = session;
            model.Profile = profile;
            model.IsNew = context.IsNew;
        }).Location("Content");

        return Combine(headerResult, contentResult);
    }
}
