using CrestApps.Core.Services;
using CrestApps.OrchardCore.Sms.Workspace.Core.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Sms.Workspace.Core.Services;

/// <summary>
/// The default implementation of <see cref="ISmsTemplateManager"/>.
/// </summary>
public sealed class SmsTemplateManager : CatalogManager<SmsTemplate>, ISmsTemplateManager
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SmsTemplateManager"/> class.
    /// </summary>
    /// <param name="store">The underlying template store.</param>
    /// <param name="handlers">The catalog entry handlers for templates.</param>
    /// <param name="logger">The logger instance.</param>
    public SmsTemplateManager(
        ISmsTemplateStore store,
        IEnumerable<ICatalogEntryHandler<SmsTemplate>> handlers,
        ILogger<CatalogManager<SmsTemplate>> logger)
        : base(store, handlers, logger)
    {
    }
}
