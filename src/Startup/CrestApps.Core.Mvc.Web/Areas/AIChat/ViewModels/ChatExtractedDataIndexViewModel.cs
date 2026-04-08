using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.Core.Mvc.Web.Areas.AIChat.ViewModels;

public sealed class ChatExtractedDataIndexViewModel
{
    public string ProfileId { get; set; }

    public DateTime? StartDateUtc { get; set; }

    public DateTime? EndDateUtc { get; set; }

    public IReadOnlyList<SelectListItem> Profiles { get; set; } = [];

    public IReadOnlyList<string> Columns { get; set; } = [];

    public IReadOnlyList<ChatExtractedDataRowViewModel> Rows { get; set; } = [];

    public bool ShowReport { get; set; }
}
