using CrestApps.Core.Services;
using CrestApps.OrchardCore.Telephony.Sms.Core.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Telephony.Sms.Core.Services;

/// <summary>
/// The default implementation of <see cref="ISmsTemplateManager"/>.
/// </summary>
public sealed class SmsTemplateManager : CatalogManager<SmsTemplate>, ISmsTemplateManager
{
    private readonly ISmsTemplateStore _store;

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
        _store = store;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<SmsTemplate>> GetEnabledAsync(CancellationToken cancellationToken = default)
    {
        var templates = await _store.GetEnabledAsync(cancellationToken);

        foreach (var template in templates)
        {
            await LoadAsync(template, cancellationToken);
        }

        return templates;
    }
}
