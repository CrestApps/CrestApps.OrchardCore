using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Chat.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.Chat.Drivers;

/// <summary>
/// Display driver for the AI chat session shape.
/// </summary>
public sealed class AIChatSessionDisplayDriver : DisplayDriver<AIChatSession>
{
    private readonly IAIProfileManager _openAIChatProfileManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIChatSessionDisplayDriver"/> class.
    /// </summary>
    /// <param name="openAIChatProfileManager">The open AI chat profile manager.</param>
    public AIChatSessionDisplayDriver(IAIProfileManager openAIChatProfileManager)
    {
        _openAIChatProfileManager = openAIChatProfileManager;
    }

    public override IDisplayResult Display(AIChatSession session, BuildDisplayContext context)
    {
        return null;
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
