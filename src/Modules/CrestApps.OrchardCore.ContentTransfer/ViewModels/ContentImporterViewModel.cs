using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.ContentTransfer.ViewModels;

public class ContentImporterViewModel
{
    /// <summary>
    /// Gets or sets the content type, or <see langword="null"/> for a target that is not a content type.
    /// </summary>
    public ContentTypeDefinition ContentTypeDefinition { get; set; }

    /// <summary>
    /// Gets or sets the name of the import target: the content type, or another target such as <c>Skills.Skill</c>.
    /// </summary>
    public string TargetName { get; set; }

    /// <summary>
    /// Gets or sets the name of the import target people see.
    /// </summary>
    public string TargetDisplayName { get; set; }

    /// <summary>
    /// Gets or sets whether the target is a content type.
    /// </summary>
    public bool IsContentType { get; set; } = true;

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
