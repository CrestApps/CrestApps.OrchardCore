using System.Text.Json;
using CrestApps.OrchardCore.AgenticCommerce.Core.Models;
using CrestApps.OrchardCore.AgenticCommerce.Orders;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Webhooks;
using CrestApps.OrchardCore.Webhooks.Models;

namespace CrestApps.OrchardCore.AgenticCommerce.Ucp;

/// <summary>
/// Sends UCP order updates to where the platform's profile says, signed with the store's key (RFC 9421).
/// </summary>
public sealed class UcpOrderPublisher : IAgentOrderPublisher
{
    private readonly AgentOrderViewBuilder _views;
    private readonly IWebhookOutbox _outbox;

    public UcpOrderPublisher(AgentOrderViewBuilder views, IWebhookOutbox outbox)
    {
        _views = views;
        _outbox = outbox;
    }

    /// <inheritdoc />
    public string Protocol
        => AgenticCommerceConstants.Protocols.Ucp;

    /// <inheritdoc />
    public async Task PublishAsync(AgentCheckout checkout, Order order, bool created, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(checkout);

        var url = checkout.GetProperty(UcpProfileClient.WebhookUrlProperty);

        if (string.IsNullOrEmpty(url))
        {
            return;
        }

        var view = await _views.BuildAsync(order, checkout, cancellationToken);
        var body = UcpOrderMapper.Write(view, checkout.ProtocolVersion ?? UcpConstants.Versions[0]).ToJsonString(JsonSerializerOptions.Web);

        await _outbox.EnqueueAsync(new WebhookDirectDelivery
        {
            OwnerId = checkout.PlatformId,
            Url = url,
            EventName = created ? "order.created" : "order.updated",
            Body = body,
            FormatterName = UcpOrderWebhookFormatter.FormatterName,
        }, cancellationToken);
    }
}
