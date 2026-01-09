using OrchardCore.ContentFields.Fields;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.AI.SmartFields.ViewModels;

public class EditSmartTextFieldViewModel
{
    public string Text { get; set; }

    public string ProfileId { get; set; }

    public string Hint { get; set; }

    public TextField Field { get; set; }

    public ContentPart Part { get; set; }

    public ContentPartFieldDefinition PartFieldDefinition { get; set; }
}
