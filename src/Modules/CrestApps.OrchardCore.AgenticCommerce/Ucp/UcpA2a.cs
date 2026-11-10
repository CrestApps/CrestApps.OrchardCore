namespace CrestApps.OrchardCore.AgenticCommerce.Ucp;

/// <summary>
/// The names and addresses of UCP's Agent2Agent (A2A) binding: the agent card, the JSON-RPC endpoint, the UCP
/// extension, the keys UCP payloads travel under in A2A data parts, and the actions the store's agent takes.
/// </summary>
public static class UcpA2a
{
    /// <summary>
    /// The JSON-RPC endpoint's path, after the tenant's base address.
    /// </summary>
    public const string Route = "ucp/a2a";

    /// <summary>
    /// The agent card's path, after the tenant's base address. It is the endpoint's base address plus A2A's well-known
    /// card path, so an A2A client that finds a card from a base address finds it.
    /// </summary>
    public const string AgentCardRoute = Route + "/.well-known/agent-card.json";

    /// <summary>
    /// The header a client activates A2A extensions with, and the agent names the activated ones in.
    /// </summary>
    public const string ExtensionsHeader = "X-A2A-Extensions";

    /// <summary>
    /// The A2A protocol version the binding speaks, the one UCP's binding is written against.
    /// </summary>
    public const string ProtocolVersion = "0.3.0";

    /// <summary>
    /// The longest <c>messageId</c> accepted, the same as UCP's longest idempotency key.
    /// </summary>
    public const int MaxMessageIdLength = 255;

    /// <summary>
    /// Returns the URI of UCP's A2A extension for a version.
    /// </summary>
    /// <param name="version">The UCP version.</param>
    public static string ExtensionUri(string version)
        => UcpConstants.Url(version, "specification/reference");

    /// <summary>
    /// The keys UCP payloads travel under in an A2A data part.
    /// </summary>
    public static class DataKeys
    {
        /// <summary>
        /// The action a request asks for.
        /// </summary>
        public const string Action = "action";

        /// <summary>
        /// The checkout, in a request and in every checkout answer.
        /// </summary>
        public const string Checkout = "a2a.ucp.checkout";

        /// <summary>
        /// The payment a checkout is completed with.
        /// </summary>
        public const string Payment = "a2a.ucp.checkout.payment";

        /// <summary>
        /// The risk signals that go with a payment.
        /// </summary>
        public const string Signals = "a2a.ucp.checkout.signals";

        /// <summary>
        /// The payment under the name UCP's sample business agent uses.
        /// </summary>
        public const string PaymentData = "a2a.ucp.checkout.payment_data";

        /// <summary>
        /// The risk signals under the name UCP's sample business agent uses.
        /// </summary>
        public const string RiskSignals = "a2a.ucp.checkout.risk_signals";

        /// <summary>
        /// An order, in an order answer. UCP's binding names only the checkout; the order follows its pattern.
        /// </summary>
        public const string Order = "a2a.ucp.order";

        /// <summary>
        /// The AP2 members: <c>checkout_mandate</c> in a request, <c>merchant_authorization</c> in an answer.
        /// </summary>
        public const string Ap2 = "ap2";

        /// <summary>
        /// The buyer's checkout mandate given as a flat key.
        /// </summary>
        public const string Ap2CheckoutMandate = "ap2.checkout_mandate";
    }

    /// <summary>
    /// The actions a data part asks the store's agent for.
    /// </summary>
    public static class Actions
    {
        /// <summary>
        /// Creates a checkout session.
        /// </summary>
        public const string CreateCheckout = "create_checkout";

        /// <summary>
        /// Returns a checkout session.
        /// </summary>
        public const string GetCheckout = "get_checkout";

        /// <summary>
        /// Replaces a checkout session's line items, buyer, fulfillment and discount codes.
        /// </summary>
        public const string UpdateCheckout = "update_checkout";

        /// <summary>
        /// Completes a checkout session with a payment.
        /// </summary>
        public const string CompleteCheckout = "complete_checkout";

        /// <summary>
        /// Cancels a checkout session.
        /// </summary>
        public const string CancelCheckout = "cancel_checkout";

        /// <summary>
        /// Adds a product to the context's checkout, or starts one with it.
        /// </summary>
        public const string AddToCheckout = "add_to_checkout";

        /// <summary>
        /// Returns an order.
        /// </summary>
        public const string GetOrder = "get_order";

        /// <summary>
        /// Every action, in the order the agent card lists them.
        /// </summary>
        public static readonly IReadOnlyList<string> All = [CreateCheckout, GetCheckout, UpdateCheckout, CompleteCheckout, CancelCheckout, AddToCheckout, GetOrder];

        /// <summary>
        /// Returns whether an action changes something, and so is run once per <c>messageId</c>.
        /// </summary>
        /// <param name="action">The action.</param>
        public static bool IsMutating(string action)
            => action is CreateCheckout or UpdateCheckout or CompleteCheckout or CancelCheckout or AddToCheckout;
    }

    /// <summary>
    /// The A2A JSON-RPC methods the binding answers.
    /// </summary>
    public static class Methods
    {
        /// <summary>
        /// Sends a message and gets the agent's answer.
        /// </summary>
        public const string SendMessage = "message/send";

        /// <summary>
        /// Sends a message and streams the answer; not offered.
        /// </summary>
        public const string StreamMessage = "message/stream";

        /// <summary>
        /// The prefix of the task methods; the agent answers with messages and keeps no tasks.
        /// </summary>
        public const string TasksPrefix = "tasks/";

        /// <summary>
        /// The prefix of the push notification methods; not offered.
        /// </summary>
        public const string PushNotificationPrefix = "tasks/pushNotificationConfig/";
    }

    /// <summary>
    /// The A2A error codes the binding answers with.
    /// </summary>
    public static class ErrorCodes
    {
        /// <summary>
        /// The task doesn't exist.
        /// </summary>
        public const int TaskNotFound = -32001;

        /// <summary>
        /// Push notifications aren't offered.
        /// </summary>
        public const int PushNotificationNotSupported = -32003;

        /// <summary>
        /// The operation isn't offered.
        /// </summary>
        public const int UnsupportedOperation = -32004;
    }
}
