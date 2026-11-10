using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AgenticCommerce.Models;
using CrestApps.OrchardCore.AgenticCommerce.Services;
using CrestApps.OrchardCore.Checkout.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.AgenticCommerce.Payments;

/// <summary>
/// Agents pay with test credentials, which the store's test payment provider takes and never charges. It is offered
/// while the test payment provider is on, so platforms can certify the store and the conformance suites can run.
/// </summary>
public sealed class TestAgentPaymentHandler : IAgentPaymentHandler
{
    /// <summary>
    /// The handler's name in every protocol.
    /// </summary>
    public const string Name = "dev.crestapps.test";

    /// <summary>
    /// The key of the test payment provider, which charges the credentials.
    /// </summary>
    public const string TestProviderKey = "test";

    internal readonly IStringLocalizer S;

    public TestAgentPaymentHandler(IStringLocalizer<TestAgentPaymentHandler> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public string GetName(string protocol)
        => Name;

    /// <inheritdoc />
    public Task<AgentPaymentHandlerDescriptor> DescribeAsync(AgentPaymentHandlerContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Only a platform in test mode pays with test credentials; a live platform's buyers pay for real.
        if (!context.TestMode)
        {
            return Task.FromResult<AgentPaymentHandlerDescriptor>(null);
        }

        return Task.FromResult(new AgentPaymentHandlerDescriptor
        {
            Id = "test",
            Name = Name,
            Version = "2026-11-01",
            Spec = "https://docs.crestapps.com/docs/modules/agentic-commerce#test-payments",
            Schema = "https://docs.crestapps.com/docs/modules/agentic-commerce#test-payments",
            Psp = "seller_managed",
            DisplayName = S["Test card"],
            InstrumentTypes = ["card"],
            Config = new JsonObject
            {
                ["environment"] = "test",
                ["tokens"] = new JsonArray { "tok_test_success", "tok_test_decline", "tok_test_3ds" },
            },
        });
    }

    /// <inheritdoc />
    public Task<AgentPaymentPreparation> PrepareAsync(AgentPaymentHandlerContext context, AgentPayment payment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(payment);

        if (!context.TestMode)
        {
            return Task.FromResult(AgentPaymentPreparation.Fail(S["Test credentials are only taken from platforms in test mode."]));
        }

        if (string.IsNullOrWhiteSpace(payment.Credential))
        {
            return Task.FromResult(AgentPaymentPreparation.Fail(S["The payment has no test credential."]));
        }

        return Task.FromResult(new AgentPaymentPreparation
        {
            Succeeded = true,
            ProviderKey = TestProviderKey,
            ProviderData = new Dictionary<string, string>
            {
                [CheckoutPaymentDataKeys.DelegatedCredential] = payment.Credential.Trim(),
                [CheckoutPaymentDataKeys.DelegatedCredentialType] = DelegatedCredentialTypes.Test,
            },
        });
    }
}
