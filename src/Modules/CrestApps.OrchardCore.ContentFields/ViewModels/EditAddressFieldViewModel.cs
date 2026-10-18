using CrestApps.OrchardCore.ContentFields.Fields;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.ContentFields.ViewModels;

public class EditAddressFieldViewModel
{
    /// <summary>
    /// Gets or sets the address editor, whose fields post under the field's prefix.
    /// </summary>
    public IShape Editor { get; set; }

    public AddressField Field { get; set; }

    public ContentPart Part { get; set; }

    public ContentPartFieldDefinition PartFieldDefinition { get; set; }
}
