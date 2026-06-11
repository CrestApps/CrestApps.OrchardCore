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
}
