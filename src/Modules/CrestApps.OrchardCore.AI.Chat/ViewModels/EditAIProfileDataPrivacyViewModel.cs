using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core.Models;

namespace CrestApps.OrchardCore.AI.Chat.ViewModels;

/// <summary>
/// What is kept of an AI profile's conversations, and for how long.
/// </summary>
public class EditAIProfileDataPrivacyViewModel
{
    /// <summary>
    /// Gets or sets what is kept of a conversation once it ends.
    /// </summary>
    public AIDataStorageMode StorageMode { get; set; }

    /// <summary>
    /// Gets or sets after how many days conversations are deleted.
    /// </summary>
    public int? RetentionDays { get; set; }
}
