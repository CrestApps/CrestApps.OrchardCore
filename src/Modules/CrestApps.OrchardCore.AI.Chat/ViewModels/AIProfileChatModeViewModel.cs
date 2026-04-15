using CrestApps.Core.AI.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.Chat.ViewModels;

public class AIProfileChatModeViewModel
{
    public ChatMode ChatMode { get; set; }

    public string VoiceName { get; set; }

    public bool EnableTextToSpeechPlayback { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> AvailableModes { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> AvailableVoices { get; set; }
}
