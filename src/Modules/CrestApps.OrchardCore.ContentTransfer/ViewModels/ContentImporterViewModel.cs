using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.ContentTransfer.ViewModels;

public class ContentImporterViewModel
{
    public ContentTypeDefinition ContentTypeDefinition { get; set; }

    public IShape Content { get; set; }

    public IEnumerable<ImportColumn> Columns { get; set; }

    /// <summary>
    /// Gets or sets the formats that write a file, offered as templates.
    /// </summary>
    public IList<SelectListItem> FileFormats { get; set; }

    /// <summary>
    /// Gets or sets the extensions of the formats files of the content type can be imported in.
    /// </summary>
    public IList<string> ImportFormats { get; set; } = [];

    /// <summary>
    /// Gets or sets the layouts of other systems the content type's import also reads, such as another store's
    /// product export.
    /// </summary>
    public IList<string> OtherLayouts { get; set; } = [];
}
