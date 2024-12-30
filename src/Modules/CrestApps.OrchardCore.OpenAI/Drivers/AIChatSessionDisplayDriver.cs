using CrestApps.OrchardCore.OpenAI.Models;
using CrestApps.OrchardCore.OpenAI.ViewModels.Sessions;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.OpenAI.Drivers;

public class AIChatSessionDisplayDriver : DisplayDriver<AIChatSession>
{
    public override IDisplayResult Display(AIChatSession session, BuildDisplayContext context)
    {
        return Initialize<DisplayAIChatSessionViewModel>("AIChatSessionListItem", m =>
        {
            m.Session = session;
        }).Location("SummaryAdmin", "Content");
    }

    public override IDisplayResult Edit(AIChatSession session, BuildEditorContext context)
    {
        var headerResult = Initialize<AIChatSessionViewModel>("AIChatSessionHeader", m =>
        {
            m.Session = session;
            m.IsNew = context.IsNew;
        }).Location("Header");

        var contentResult = Initialize<AIChatSessionViewModel>("AIChatSessionChat", m =>
        {
            m.Session = session;
            m.IsNew = context.IsNew;
        }).Location("Content");

        return Combine(headerResult, contentResult);
    }
}
