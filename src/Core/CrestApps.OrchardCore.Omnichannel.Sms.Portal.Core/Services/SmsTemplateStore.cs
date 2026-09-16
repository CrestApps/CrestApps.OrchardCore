using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services;

/// <summary>
/// A YesSql-based implementation of <see cref="ISmsTemplateStore"/>.
/// </summary>
public sealed class SmsTemplateStore : DocumentCatalog<SmsTemplate, SmsTemplateIndex>, ISmsTemplateStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SmsTemplateStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public SmsTemplateStore(ISession session)
        : base(session)
    {
        CollectionName = SmsPortalStorage.CollectionName;
    }
}
