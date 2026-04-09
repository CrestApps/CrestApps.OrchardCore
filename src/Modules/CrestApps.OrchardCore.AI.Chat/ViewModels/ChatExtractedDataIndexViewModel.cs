using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.Chat.ViewModels;

public class ChatExtractedDataIndexViewModel
{
    public string ProfileId { get; set; }

    public DateTime? StartDateUtc { get; set; }

    public DateTime? EndDateUtc { get; set; }

    public IReadOnlyList<SelectListItem> Profiles { get; set; } = [];

    public IReadOnlyList<string> Columns { get; set; } = [];

    public IReadOnlyList<ChatExtractedDataRowViewModel> Rows { get; set; } = [];

    public bool ShowReport { get; set; }
}
