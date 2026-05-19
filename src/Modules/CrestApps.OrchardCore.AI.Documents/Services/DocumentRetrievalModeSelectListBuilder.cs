using CrestApps.Core.AI.Documents.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.AI.Documents.Services;

internal static class DocumentRetrievalModeSelectListBuilder
{
    public static IList<SelectListItem> Build(
        IStringLocalizer stringLocalizer,
        DocumentRetrievalMode? selectedMode)
    {
        return
        [
            new(stringLocalizer["Chunk"], nameof(DocumentRetrievalMode.Chunk))
            {
                Selected = selectedMode == DocumentRetrievalMode.Chunk,
            },
            new(stringLocalizer["Hierarchical"], nameof(DocumentRetrievalMode.Hierarchical))
            {
                Selected = selectedMode == DocumentRetrievalMode.Hierarchical,
            },
        ];
    }
}
