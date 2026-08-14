using CrestApps.OrchardCore.Omnichannel.Core;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

internal static class OmnichannelSubjectDefinitionService
{
    public static bool HasOmnichannelSubjectPart(ContentTypeDefinition contentTypeDefinition)
    {
        return contentTypeDefinition?.Parts.Any(part =>
            part.Name == OmnichannelConstants.ContentParts.OmnichannelSubject) == true;
    }
}
