using CrestApps.OrchardCore.AI.Chat.ViewModels;
using CrestApps.OrchardCore.AI.Models;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.Chat.Drivers;

public sealed class AIChatSessionDisplayDriver : DisplayDriver<AIChatSession>
{
    private readonly IAIProfileManager _openAIChatProfileManager;

    public AIChatSessionDisplayDriver(IAIProfileManager openAIChatProfileManager)
    {
        _openAIChatProfileManager = openAIChatProfileManager;
    }

    public override IDisplayResult Display(AIChatSession session, BuildDisplayContext context)
    {
        return Initialize<DisplayAIChatSessionViewModel>("AIChatSessionListItem", model =>
        {
            model.Session = session;
        }).Location("SummaryAdmin", "Content");
    }

    public override async Task<IDisplayResult> EditAsync(AIChatSession session, BuildEditorContext context)
    {
        var profile = await _openAIChatProfileManager.FindByIdAsync(session.ProfileId);

        if (profile == null)
        {
            return null;
        }

        var headerResult = Initialize<ChatSessionCapsuleViewModel>("AIChatSessionHeader", model =>
        {
            model.Session = session;
            model.Profile = profile;
            model.IsNew = context.IsNew;
        }).Location("Header");

        var contentResult = Initialize<ChatSessionCapsuleViewModel>("AIChatSessionChat", model =>
        {
            model.Session = session;
            model.Profile = profile;
            model.IsNew = context.IsNew;
        }).Location("Content");

        return Combine(headerResult, contentResult);
    }
}
