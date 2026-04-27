using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.Chat.ViewModels;

/// <summary>
/// Represents the view model for chat extracted data index.
/// </summary>
public class ChatExtractedDataIndexViewModel
{
    /// <summary>
    /// Gets or sets the profile id.
    /// </summary>
    public string ProfileId { get; set; }

    /// <summary>
    /// Gets or sets the start date utc.
    /// </summary>
    public DateTime? StartDateUtc { get; set; }

    /// <summary>
    /// Gets or sets the end date utc.
    /// </summary>
    public DateTime? EndDateUtc { get; set; }

    /// <summary>
    /// Gets or sets the profiles.
    /// </summary>
    public IReadOnlyList<SelectListItem> Profiles { get; set; } = [];

    /// <summary>
    /// Gets or sets the columns.
    /// </summary>
    public IReadOnlyList<string> Columns { get; set; } = [];

    /// <summary>
    /// Gets or sets the rows.
    /// </summary>
    public IReadOnlyList<ChatExtractedDataRowViewModel> Rows { get; set; } = [];

    /// <summary>
    /// Gets or sets the show report.
    /// </summary>
    public bool ShowReport { get; set; }
}
