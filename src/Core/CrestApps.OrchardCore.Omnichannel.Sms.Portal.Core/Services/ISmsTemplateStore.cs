using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services;

/// <summary>
/// The persistence contract for <see cref="SmsTemplate"/>.
/// </summary>
public interface ISmsTemplateStore : ICatalog<SmsTemplate>
{
}
