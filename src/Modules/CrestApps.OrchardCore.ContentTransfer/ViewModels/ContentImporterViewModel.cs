using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.DisplayManagement;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.ContentTransfer.ViewModels;

public class ContentImporterViewModel
{
    public ContentTypeDefinition ContentTypeDefinition { get; set; }

    public IShape Content { get; set; }

    public IEnumerable<ImportColumn> Columns { get; set; }

    public IList<SelectListItem> FileFormats { get; set; }
}
