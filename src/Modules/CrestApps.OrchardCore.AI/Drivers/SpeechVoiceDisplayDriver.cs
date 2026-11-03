using CrestApps.Core.AI.Models;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.Drivers;

/// <summary>
/// Displays a voice in the voice library.
/// </summary>
public sealed class SpeechVoiceDisplayDriver : DisplayDriver<SpeechVoice>
{
    /// <inheritdoc/>
    public override Task<IDisplayResult> DisplayAsync(SpeechVoice voice, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
        }

        return CombineAsync(
            View("SpeechVoice_Fields_SummaryAdmin", voice)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("SpeechVoice_DefaultTags_SummaryAdmin", voice)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Language:5"),
            View("SpeechVoice_Buttons_SummaryAdmin", voice).Location("Actions:5"));
    }
}
