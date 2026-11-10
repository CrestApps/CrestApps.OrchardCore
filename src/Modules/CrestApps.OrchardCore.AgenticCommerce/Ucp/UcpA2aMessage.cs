using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AgenticCommerce.Core.Security;
using CrestApps.OrchardCore.AgenticCommerce.Endpoints;

namespace CrestApps.OrchardCore.AgenticCommerce.Ucp;

/// <summary>
/// An A2A message a platform sent the store's agent, read for UCP: its ids, its text, and its data parts merged into one
/// object, from which the action, the checkout, the payment and the AP2 mandate are read.
/// </summary>
public sealed class UcpA2aMessage
{
    /// <summary>
    /// Gets the message's id, the idempotency key of a request that changes something.
    /// </summary>
    public string MessageId { get; private init; }

    /// <summary>
    /// Gets the conversation's id, or <see langword="null"/> on the first turn.
    /// </summary>
    public string ContextId { get; private init; }

    /// <summary>
    /// Gets the message's data parts merged into one object; a key in a later part replaces the same key in an earlier
    /// one.
    /// </summary>
    public JsonObject Data { get; private init; } = [];

    /// <summary>
    /// Gets a value indicating whether the message has a data part.
    /// </summary>
    public bool HasData { get; private init; }

    /// <summary>
    /// Gets the message's text parts, one per line.
    /// </summary>
    public string Text { get; private init; }

    /// <summary>
    /// Gets the message's parts as sent.
    /// </summary>
    public JsonArray Parts { get; private init; } = [];

    /// <summary>
    /// Gets the action the message asks for, or <see langword="null"/>.
    /// </summary>
    public string Action
        => AgentJson.String(Data, UcpA2a.DataKeys.Action);

    /// <summary>
    /// Gets the checkout's id the message names: <c>id</c> or <c>checkout_id</c> beside the action, or the checkout's
    /// own <c>id</c>.
    /// </summary>
    public string CheckoutId
        => NonEmpty(AgentJson.String(Data, "id")) ??
            NonEmpty(AgentJson.String(Data, "checkout_id")) ??
            NonEmpty(AgentJson.String(Data[UcpA2a.DataKeys.Checkout], "id"));

    /// <summary>
    /// Gets the order's id the message names: <c>id</c> or <c>order_id</c> beside the action, or the order's own
    /// <c>id</c>.
    /// </summary>
    public string OrderId
        => NonEmpty(AgentJson.String(Data, "id")) ??
            NonEmpty(AgentJson.String(Data, "order_id")) ??
            NonEmpty(AgentJson.String(Data[UcpA2a.DataKeys.Order], "id"));

    /// <summary>
    /// Reads an A2A message, or returns <see langword="null"/> when it isn't a JSON object.
    /// </summary>
    /// <param name="node">The message: <c>params.message</c> of <c>message/send</c>.</param>
    public static UcpA2aMessage Parse(JsonNode node)
    {
        if (node is not JsonObject message)
        {
            return null;
        }

        var data = new JsonObject();
        var text = new StringBuilder();
        var hasData = false;
        var parts = message["parts"] as JsonArray ?? [];

        foreach (var part in parts.OfType<JsonObject>())
        {
            if (IsDataPart(part))
            {
                hasData = true;

                foreach (var (name, value) in (JsonObject)part["data"])
                {
                    data[name] = value?.DeepClone();
                }
            }
            else if (AgentJson.String(part, "text") is { } line)
            {
                if (text.Length > 0)
                {
                    text.Append('\n');
                }

                text.Append(line);
            }
        }

        return new UcpA2aMessage
        {
            MessageId = NonEmpty(AgentJson.String(message, "messageId")),
            ContextId = NonEmpty(AgentJson.String(message, "contextId")),
            Data = data,
            HasData = hasData,
            Text = text.ToString(),
            Parts = (JsonArray)parts.DeepClone(),
        };
    }

    /// <summary>
    /// Returns whether a part is a data part: its kind (A2A 0.3) or type (UCP's examples) is <c>data</c>, or it has a
    /// <c>data</c> object and no kind.
    /// </summary>
    /// <param name="part">The part.</param>
    public static bool IsDataPart(JsonObject part)
    {
        ArgumentNullException.ThrowIfNull(part);

        if (part["data"] is not JsonObject)
        {
            return false;
        }

        var kind = AgentJson.String(part, "kind") ?? AgentJson.String(part, "type");

        return kind is null || string.Equals(kind, "data", StringComparison.Ordinal);
    }

    /// <summary>
    /// Returns the message's fingerprint: SHA-256 of its parts in RFC 8785 canonical JSON, so a retry with the same
    /// <c>messageId</c> matches by meaning, not by spacing or member order.
    /// </summary>
    public string GetFingerprint()
    {
        byte[] canonical;

        try
        {
            canonical = JsonCanonicalizer.CanonicalizeToUtf8(Parts);
        }
        catch (ArgumentException)
        {
            // A string with a lone surrogate has no canonical form; its text as parsed still tells requests apart.
            canonical = Encoding.UTF8.GetBytes(Parts.ToJsonString());
        }

        return Convert.ToHexStringLower(SHA256.HashData(canonical));
    }

    /// <summary>
    /// Returns the request the REST binding would take: the checkout, with the payment, the signals and the AP2 mandate
    /// moved where the REST binding reads them.
    /// </summary>
    public JsonObject ToCheckoutBody()
    {
        var body = Data[UcpA2a.DataKeys.Checkout] is JsonObject checkout ? (JsonObject)checkout.DeepClone() : [];

        // UCP's binding sends the payment; its AP2 example sends the instrument itself.
        if ((Data[UcpA2a.DataKeys.Payment] ?? Data[UcpA2a.DataKeys.PaymentData]) is JsonObject payment)
        {
            body["payment"] = payment["instruments"] is JsonArray
                ? payment.DeepClone()
                : new JsonObject { ["instruments"] = new JsonArray(payment.DeepClone()) };
        }

        if ((Data[UcpA2a.DataKeys.Signals] ?? Data[UcpA2a.DataKeys.RiskSignals]) is JsonObject signals)
        {
            body["signals"] = signals.DeepClone();
        }

        if (Data[UcpA2a.DataKeys.Ap2] is JsonObject ap2)
        {
            body["ap2"] = ap2.DeepClone();
        }
        else if (AgentJson.String(Data, UcpA2a.DataKeys.Ap2CheckoutMandate) is { } mandate)
        {
            body["ap2"] = new JsonObject { ["checkout_mandate"] = mandate };
        }

        return body;
    }

    private static string NonEmpty(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value;
}
