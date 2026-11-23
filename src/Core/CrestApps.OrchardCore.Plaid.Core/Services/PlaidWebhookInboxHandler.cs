using System.Runtime.ExceptionServices;
using System.Text.Json;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Plaid.Models;
using CrestApps.OrchardCore.Webhooks;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Plaid.Services;

/// <summary>
/// Hands the Plaid webhooks the provider webhook inbox stored to every <see cref="IPlaidWebhookHandler"/>. The webhook
/// endpoint checked the signature once, before the webhook was stored, so nothing here checks it again. Every handler
/// runs even when another one throws; the failure is then thrown so the inbox dispatches the webhook again, which is why
/// every handler must be idempotent.
/// </summary>
public sealed class PlaidWebhookInboxHandler : IProviderWebhookInboxHandler
{
    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web);

    private readonly IEnumerable<IPlaidWebhookHandler> _handlers;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaidWebhookInboxHandler"/> class.
    /// </summary>
    /// <param name="handlers">The handlers every webhook is handed to.</param>
    /// <param name="logger">The logger.</param>
    public PlaidWebhookInboxHandler(
        IEnumerable<IPlaidWebhookHandler> handlers,
        ILogger<PlaidWebhookInboxHandler> logger)
    {
        _handlers = handlers;
        _logger = logger;
    }

    /// <inheritdoc />
    public string TechnicalName
        => PlaidConstants.WebhookHandlerName;

    /// <inheritdoc />
    public WebhookHandlerReplaySafety ReplaySafety
        => WebhookHandlerReplaySafety.NaturallyIdempotent;

    /// <inheritdoc />
    public async Task HandleAsync(string payload, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(payload);

        var webhook = JsonSerializer.Deserialize<PlaidWebhook>(payload, _options)
            ?? throw new InvalidDataException("The Plaid webhook payload could not be read.");

        var context = new PlaidWebhookContext(webhook, payload);
        List<Exception> failures = null;

        foreach (var handler in _handlers)
        {
            try
            {
                await handler.HandleAsync(context, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                _logger.LogError(
                    exception,
                    "The Plaid webhook handler {Handler} failed on the webhook {Type} {Code} for item {ItemId}; the webhook will be dispatched again.",
                    handler.GetType().Name,
                    webhook.WebhookType.SanitizeLogValue(),
                    webhook.WebhookCode.SanitizeLogValue(),
                    webhook.ItemId.SanitizeLogValue());

                (failures ??= []).Add(exception);
            }
        }

        if (failures is null)
        {
            return;
        }

        if (failures.Count == 1)
        {
            ExceptionDispatchInfo.Throw(failures[0]);
        }

        throw new AggregateException("Several Plaid webhook handlers failed.", failures);
    }
}
