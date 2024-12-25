using CrestApps.OrchardCore.OpenAI.Core.Models;
using CrestApps.OrchardCore.OpenAI.ViewModels.Sessions;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.OpenAI.Drivers;

public class AIChatSessionDisplayDriver : DisplayDriver<AIChatSession>
{
    public override IDisplayResult Edit(AIChatSession session, BuildEditorContext context)
    {
        var headerResult = Initialize<AIChatSessionViewModel>("AIChatSessionHeader", m =>
        {
            m.Session = session;
        }).Location("Header");

        var contentResult = Initialize<AIChatSessionViewModel>("AIChatSessionBody", m =>
        {
            m.Session = session;
        }).Location("Content");

        var formResult = Initialize<AIChatSessionViewModel>("AIChatSessionForm", m =>
        {
            m.Session = session;
        }).Location("Footer");

        return Combine(headerResult, contentResult, formResult);
    }
}
