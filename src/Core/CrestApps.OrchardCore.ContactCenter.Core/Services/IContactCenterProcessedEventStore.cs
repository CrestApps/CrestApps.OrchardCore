using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Defines the persistence contract for event deduplication markers.
/// </summary>
public interface IContactCenterProcessedEventStore : ICatalog<ContactCenterProcessedEvent>
{
}
