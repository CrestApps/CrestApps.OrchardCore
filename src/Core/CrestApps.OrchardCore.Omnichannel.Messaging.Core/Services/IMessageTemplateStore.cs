using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;

/// <summary>
/// The persistence contract for <see cref="MessageTemplate"/>.
/// </summary>
public interface IMessageTemplateStore : ICatalog<MessageTemplate>
{
}
