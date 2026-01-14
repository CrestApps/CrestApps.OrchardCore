using OrchardCore.ContentFields.Fields;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

public sealed class OmnichannelContactInfoPart : ContentPart
{
    public TextField FirstName { get; set; }

    public TextField LastName { get; set; }
}
