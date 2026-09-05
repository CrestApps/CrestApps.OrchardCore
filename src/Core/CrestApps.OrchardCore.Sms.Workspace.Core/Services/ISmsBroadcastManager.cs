using CrestApps.Core.Services;
using CrestApps.OrchardCore.Sms.Workspace.Core.Models;

namespace CrestApps.OrchardCore.Sms.Workspace.Core.Services;

/// <summary>
/// The management contract for <see cref="SmsBroadcast"/>.
/// </summary>
public interface ISmsBroadcastManager : ICatalogManager<SmsBroadcast>
{
}
