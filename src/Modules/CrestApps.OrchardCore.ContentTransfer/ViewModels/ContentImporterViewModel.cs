using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.ContentTransfer.ViewModels;

public class ContentImporterViewModel
{
    public ContentTypeDefinition ContentTypeDefinition { get; set; }

    public IShape Content { get; set; }

    public IEnumerable<ImportColumn> Columns { get; set; }

    public IList<SelectListItem> FileFormats { get; set; }

    /// <summary>
    /// Gets or sets the layouts of other systems the content type's import also reads, such as another store's
    /// product export.
    /// </summary>
    public IList<string> OtherLayouts { get; set; } = [];
}
