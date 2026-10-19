using CrestApps.OrchardCore.Customers.PersonalData;
using CrestApps.OrchardCore.Inventory.Core;
using CrestApps.OrchardCore.Inventory.Core.Models;
using YesSql;
using YesSql.Services;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Inventory.Services;

/// <summary>
/// Exports a person's back-in-stock requests, found by their email addresses or their account, and deletes them when
/// the person's data is anonymized: a request is nothing but who to email.
/// </summary>
public sealed class StockAlertPersonalDataContributor : ICommercePersonalDataContributor
{
    private readonly ISession _session;

    public StockAlertPersonalDataContributor(ISession session)
    {
        _session = session;
    }

    public Task IdentifyAsync(CommercePersonalDataSubject subject, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public async Task ExportAsync(CommercePersonalDataExportContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var alert in await FindAsync(context.Subject, cancellationToken))
        {
            context.Export.StockAlerts.Add(new CommerceStockAlertRecord
            {
                ContentItemId = alert.ContentItemId,
                VariantId = alert.VariantId,
                Email = alert.Email,
                CreatedUtc = alert.CreatedUtc,
                SentUtc = alert.SentUtc,
            });
        }
    }

    public async Task AnonymizeAsync(CommercePersonalDataAnonymizeContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var alert in await FindAsync(context.Subject, cancellationToken))
        {
            _session.Delete(alert, InventoryConstants.CollectionName);
            context.Result.StockAlerts++;
        }
    }

    private async Task<IReadOnlyList<StockAlert>> FindAsync(CommercePersonalDataSubject subject, CancellationToken cancellationToken)
    {
        var emails = subject.Emails.Select(email => email.Trim().ToUpperInvariant()).Where(email => email.Length > 0).ToArray();
        var userIds = subject.UserIds.Where(id => !string.IsNullOrEmpty(id)).ToArray();
        var found = new Dictionary<long, StockAlert>();

        if (emails.Length > 0)
        {
            foreach (var alert in await _session.Query<StockAlert, StockAlertIndex>(index => index.NormalizedEmail.IsIn(emails), InventoryConstants.CollectionName).ListAsync(cancellationToken))
            {
                found[alert.Id] = alert;
            }
        }

        // A signed-in shopper's requests under another address are theirs too.
        if (userIds.Length > 0)
        {
            foreach (var alert in await _session.Query<StockAlert, StockAlertIndex>(index => index.UserId.IsIn(userIds), InventoryConstants.CollectionName).ListAsync(cancellationToken))
            {
                found[alert.Id] = alert;
            }
        }

        return [.. found.Values];
    }
}
