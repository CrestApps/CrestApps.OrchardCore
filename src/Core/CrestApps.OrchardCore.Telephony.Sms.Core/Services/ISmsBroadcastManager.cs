using CrestApps.Core.Services;
using CrestApps.OrchardCore.Telephony.Sms.Core.Models;

namespace CrestApps.OrchardCore.Telephony.Sms.Core.Services;

/// <summary>
/// The management contract for <see cref="SmsBroadcast"/>.
/// </summary>
public interface ISmsBroadcastManager : ICatalogManager<SmsBroadcast>
{
}
