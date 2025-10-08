using OrchardCore.ContentFields.Fields;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Omnichannel.Core.Models;


public sealed class PhoneNumberInfoPart : ContentPart
{
    public TextField Number { get; set; }

    public TextField Extension { get; set; }

    public TextField Type { get; set; }
}
