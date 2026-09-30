using CrestApps.OrchardCore.Omnichannel.Managements.Workflows.Models;
using OrchardCore.Workflows.Display;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Workflows.Drivers;

/// <summary>
/// Display driver for the <see cref="LeadConvertedEvent"/> workflow activity. The event has no settings, so it only
/// renders its thumbnail and design shapes.
/// </summary>
public sealed class LeadConvertedEventDisplayDriver : ActivityDisplayDriver<LeadConvertedEvent>
{
}
