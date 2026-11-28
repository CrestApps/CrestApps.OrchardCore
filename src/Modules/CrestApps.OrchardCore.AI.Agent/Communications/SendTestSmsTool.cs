using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore;
using OrchardCore.Sms;

namespace CrestApps.OrchardCore.AI.Agent.Communications;

/// <summary>
/// Sends the fixed test text message through a chosen SMS provider, as the SMS Test admin page does: the provider is
/// resolved by its name and sends the message itself. The text is fixed, so the tool checks the SMS settings and
/// cannot be used to write a message to somebody. Nothing of the provider settings is returned.
/// </summary>
public sealed class SendTestSmsTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "sendTestSms";

    /// <summary>
    /// The text of the test message, the one the SMS Test admin page sends.
    /// </summary>
    public const string Body = "This is a test SMS message.";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "phoneNumber": {
          "type": "string",
          "description": "The phone number the test message goes to, in international format, for example +15551234567."
        },
        "provider": {
          "type": "string",
          "description": "The technical name of the SMS provider to test, for example Twilio. It can be left out when one provider only is enabled. A call with an unknown name sends nothing and returns the providers that are enabled."
        }
      },
      "required": [
        "phoneNumber"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Sends the fixed test text message to a phone number through a chosen SMS provider and returns the provider's result or error. Each message costs money; use it only to check SMS settings, and only to the number the person names.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    /// <inheritdoc/>
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var services = arguments.Services;

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, SmsPermissions.ManageSmsSettings))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        // The providers the SMS Test page offers: the enabled ones. A settings save refreshes these options without a
        // restart, so they are read through the monitor.
        var availableProviders = services.GetRequiredService<IOptionsMonitor<SmsProviderOptions>>().CurrentValue.Providers
            .Where(entry => entry.Value.IsEnabled)
            .Select(entry => entry.Key)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (availableProviders.Count == 0)
        {
            return "Nothing was sent: no SMS provider is enabled. A person enables one under Settings, Communication, SMS.";
        }

        arguments.TryGetFirstString("provider", out var providerName);

        // The page makes the person pick the provider. One enabled provider is the only pick there is.
        providerName = string.IsNullOrWhiteSpace(providerName) && availableProviders.Count == 1
            ? availableProviders[0]
            : availableProviders.FirstOrDefault(candidate => string.Equals(candidate, providerName?.Trim(), StringComparison.OrdinalIgnoreCase));

        var provider = providerName is null
            ? null
            : await services.GetRequiredService<ISmsProviderResolver>().GetAsync(providerName);

        if (provider is null)
        {
            return AgentToolResults.Json(new
            {
                sent = false,
                error = "Nothing was sent: give provider with the name of one of the enabled SMS providers.",
                availableProviders,
            });
        }

        if (!arguments.TryGetFirstString("phoneNumber", out var phoneNumber) ||
            !services.GetRequiredService<IPhoneFormatValidator>().IsValid(phoneNumber.Trim()))
        {
            return "Nothing was sent: give phoneNumber as a valid phone number in international format, for example +15551234567.";
        }

        phoneNumber = phoneNumber.Trim();

        var message = new SmsMessage
        {
            To = phoneNumber,
            Body = Body,
        };

        try
        {
            var result = await provider.SendAsync(message, cancellationToken);

            return AgentToolResults.Json(new
            {
                sent = result.Succeeded,
                provider = providerName,
                to = phoneNumber,
                body = Body,
                errors = result.Succeeded
                    ? null
                    : result.Errors.Select(error => error.Message?.Value).Where(error => !string.IsNullOrEmpty(error)).ToList(),
                note = result.Succeeded
                    ? "The provider accepted the message. Ask the person to check that it arrived."
                    : "The test SMS message failed to send. Check the SMS settings under Settings, Communication, SMS.",
            });
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // What the provider threw is logged and not returned: it can name accounts.
            services.GetService<ILogger<SendTestSmsTool>>()?.LogError(exception, "The test SMS message could not be sent through the provider '{Provider}'.", providerName);

            return AgentToolResults.Json(new
            {
                sent = false,
                provider = providerName,
                error = "The test SMS message failed to send. The log of the site has the details.",
                availableProviders,
            });
        }
    }
}
