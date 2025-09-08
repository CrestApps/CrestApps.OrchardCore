using OrchardCore.ContentFields.Fields;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

public sealed class EmailInfoPart : ContentPart
{
    public TextField Email { get; set; }
}
