using System.Text.Json;

using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.Core.Services;

namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Represents an action that executes when a subject disposition is selected during activity completion.
/// </summary>
public sealed class SubjectAction : SourceCatalogEntry, IDisplayTextAwareModel, ICloneable<SubjectAction>
{
    /// <summary>
    /// Gets or sets the display text for this action.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets the subject content type this action belongs to.
    /// </summary>
    public string SubjectContentType { get; set; }

    /// <summary>
    /// Gets or sets the disposition identifier that triggers this action.
    /// </summary>
    public string DispositionId { get; set; }

    /// <summary>
    /// Gets or sets whether to set the contact's "Do Not Call" preference when this action executes.
    /// </summary>
    public bool? SetDoNotCall { get; set; }

    /// <summary>
    /// Gets or sets whether to set the contact's "Do Not SMS" preference when this action executes.
    /// </summary>
    public bool? SetDoNotSms { get; set; }

    /// <summary>
    /// Gets or sets whether to set the contact's "Do Not Email" preference when this action executes.
    /// </summary>
    public bool? SetDoNotEmail { get; set; }

    /// <summary>
    /// Gets or sets whether to set the contact's "Do Not Chat" preference when this action executes.
    /// </summary>
    public bool? SetDoNotChat { get; set; }

    /// <summary>
    /// Gets or sets the date and time the action was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the author who created this action.
    /// </summary>
    public string Author { get; set; }

    /// <summary>
    /// Gets or sets the owner identifier.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Creates a copy of the current subject action.
    /// </summary>
    public SubjectAction Clone()
    {
        return new SubjectAction
        {
            ItemId = ItemId,
            Source = Source,
            DisplayText = DisplayText,
            SubjectContentType = SubjectContentType,
            DispositionId = DispositionId,
            SetDoNotCall = SetDoNotCall,
            SetDoNotSms = SetDoNotSms,
            SetDoNotEmail = SetDoNotEmail,
            SetDoNotChat = SetDoNotChat,
            CreatedUtc = CreatedUtc,
            Author = Author,
            OwnerId = OwnerId,
            Properties = Properties is null
                ? null
                : JsonSerializer.Deserialize<Dictionary<string, object>>(JsonSerializer.Serialize(Properties)),
        };
    }
}
