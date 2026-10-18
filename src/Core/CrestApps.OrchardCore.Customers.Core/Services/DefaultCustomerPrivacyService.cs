using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Customers.Core.Models;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Customers.Core.Services;

public sealed class DefaultCustomerPrivacyService : ICustomerPrivacyService
{
    private static readonly JsonSerializerOptions _exportOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly ICustomerStore _store;
    private readonly IEnumerable<ICustomerPrivacyHandler> _handlers;
    private readonly IClock _clock;

    public DefaultCustomerPrivacyService(ICustomerStore store, IEnumerable<ICustomerPrivacyHandler> handlers, IClock clock)
    {
        _store = store;
        // A stable sort, so handlers with the same order keep their registration order.
        _handlers = handlers.OrderBy(handler => handler.Order).ToArray();
        _clock = clock;
    }

    public Task EraseAsync(Customer customer, string by, CancellationToken cancellationToken = default)
        => AnonymizeAsync(customer, CustomerPrivacyAction.Erased, by, cancellationToken);

    public Task ExpireAsync(Customer customer, CancellationToken cancellationToken = default)
        => AnonymizeAsync(customer, CustomerPrivacyAction.Expired, by: null, cancellationToken);

    public async Task<JsonObject> ExportAsync(Customer customer, string by, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(customer);

        var now = _clock.UtcNow;
        var export = new JsonObject
        {
            ["exportedUtc"] = now,
            ["customer"] = JsonSerializer.SerializeToNode(new
            {
                customer.ItemId,
                Kind = customer.Kind.ToString(),
                Status = customer.Status.ToString(),
                customer.UserId,
                customer.DisplayName,
                customer.FirstName,
                customer.LastName,
                customer.CompanyName,
                customer.TaxId,
                customer.Email,
                customer.Phone,
                customer.AcceptsMarketing,
                customer.CreatedUtc,
                customer.LastActivityUtc,
                customer.MergedCustomerIds,
            }, _exportOptions),
            ["addresses"] = JsonSerializer.SerializeToNode(customer.Addresses ?? [], _exportOptions),
        };

        var context = new CustomerExportContext(customer, export);

        foreach (var handler in _handlers)
        {
            await handler.ExportingAsync(context, cancellationToken);
        }

        customer.PrivacyLog ??= [];
        customer.PrivacyLog.Add(new CustomerPrivacyEntry { Utc = now, Action = CustomerPrivacyAction.Exported, By = by });

        await _store.UpdateAsync(customer, cancellationToken);

        return export;
    }

    private async Task AnonymizeAsync(Customer customer, CustomerPrivacyAction action, string by, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(customer);

        if (customer.Status == CustomerStatus.Anonymized)
        {
            return;
        }

        var now = _clock.UtcNow;

        // The handlers see the account the customer belonged to before it is unlinked.
        var context = new CustomerPrivacyContext(customer, customer.UserId);

        foreach (var handler in _handlers)
        {
            await handler.ErasingAsync(context, cancellationToken);
        }

        customer.Anonymize(now);
        customer.PrivacyLog ??= [];
        customer.PrivacyLog.Add(new CustomerPrivacyEntry { Utc = now, Action = action, By = by });

        await _store.UpdateAsync(customer, cancellationToken);
    }
}
