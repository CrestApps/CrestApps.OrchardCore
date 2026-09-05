using CrestApps.OrchardCore.Sms.Workspace.Core.Indexes;
using CrestApps.OrchardCore.Sms.Workspace.Core.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Sms.Workspace.Core.Services;

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
        CollectionName = SmsWorkspaceStorage.CollectionName;
    }
}
