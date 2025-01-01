using CrestApps.OrchardCore.OpenAI.Models;
using CrestApps.OrchardCore.OpenAI.ViewModels.Sessions;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.OpenAI.Drivers;

public class AIChatSessionDisplayDriver : DisplayDriver<OpenAIChatSession>
{
    public override IDisplayResult Display(OpenAIChatSession session, BuildDisplayContext context)
    {
        return Initialize<DisplayAIChatSessionViewModel>("OpenAIChatSessionListItem", m =>
        {
            m.Session = session;
        }).Location("SummaryAdmin", "Content");
    }

    public override IDisplayResult Edit(OpenAIChatSession session, BuildEditorContext context)
    {
        var headerResult = Initialize<ChatSessionCapsuleViewModel>("OpenAIChatSessionHeader", m =>
        {
            m.Session = session;
            m.IsNew = context.IsNew;
        }).Location("Header");

        var contentResult = Initialize<ChatSessionCapsuleViewModel>("OpenAIChatSessionChat", m =>
        {
            m.Session = session;
            m.IsNew = context.IsNew;
        }).Location("Content");

        return Combine(headerResult, contentResult);
    }
}
