using CrestApps.Core.Services;
using CrestApps.OrchardCore.Sms.Workspace.Core.Models;

namespace CrestApps.OrchardCore.Sms.Workspace.Core.Services;

/// <summary>
/// The persistence contract for <see cref="SmsTemplate"/>.
/// </summary>
public interface ISmsTemplateStore : ICatalog<SmsTemplate>
{
}
