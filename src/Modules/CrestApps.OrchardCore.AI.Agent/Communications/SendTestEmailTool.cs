using System.Net.Mail;
using System.Security.Claims;
using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Email;
using OrchardCore.Email.Services;
using OrchardCore.Users;
using MailMessage = OrchardCore.Email.MailMessage;

namespace CrestApps.OrchardCore.AI.Agent.Communications;

/// <summary>
/// Sends a test email through the email service and a chosen provider, as the Email Test admin page does. The page
/// lets a person type the subject and the body; the tool sends a fixed subject and body to one address, so it checks
/// the email settings and cannot be used to write an email to somebody. Nothing of the provider settings is returned.
/// </summary>
public sealed class SendTestEmailTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "sendTestEmail";

    /// <summary>
    /// The subject of the test email.
    /// </summary>
    public const string Subject = "Test email";

    /// <summary>
    /// The body of the test email.
    /// </summary>
    public const string Body = "This is a test email. It was sent to check the email settings of the site.";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "to": {
          "type": "string",
          "description": "The one email address the test email goes to. Leave it out to send it to the email address of the calling user."
        },
        "provider": {
          "type": "string",
          "description": "The technical name of the email provider to test, for example SMTP. Leave it out to test the default provider. A call with an unknown name sends nothing and returns the providers that are enabled."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Sends a test email with a fixed subject and body through a chosen email provider (or the default one) and returns the provider's result or error. Use it to check email settings after changing them; the result lists the available providers. It sends to the calling user's own address unless another address is given.";

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
        var caller = AgentToolAuthorization.GetCaller(services);

        if (caller is null || !await AgentToolAuthorization.IsAuthorizedAsync(services, EmailPermissions.ManageEmailSettings))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        // The providers the Email Test page offers: the enabled ones, with the name each shows.
        var resolver = services.GetRequiredService<IEmailProviderResolver>();
        var defaultProvider = services.GetService<IOptions<EmailOptions>>()?.Value.DefaultProviderName;
        var providers = new List<(string Name, string DisplayName)>();

        foreach (var entry in services.GetRequiredService<IOptions<EmailProviderOptions>>().Value.Providers)
        {
            if (entry.Value.IsEnabled)
            {
                providers.Add((entry.Key, (await resolver.GetAsync(entry.Key))?.DisplayName?.Value ?? entry.Key));
            }
        }

        var availableProviders = providers
            .OrderBy(provider => provider.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(provider => new
            {
                name = provider.Name,
                displayName = provider.DisplayName,
                isDefault = string.Equals(provider.Name, defaultProvider, StringComparison.OrdinalIgnoreCase)
                    ? true
                    : (bool?)null,
            })
            .ToList();

        if (providers.Count == 0)
        {
            return "Nothing was sent: no email provider is enabled. A person enables one under Settings, Communication, Email.";
        }

        arguments.TryGetFirstString("provider", out var providerName);

        providerName = string.IsNullOrWhiteSpace(providerName)
            ? defaultProvider
            : providerName.Trim();

        var provider = providers.FirstOrDefault(candidate => string.Equals(candidate.Name, providerName, StringComparison.OrdinalIgnoreCase));

        if (provider.Name is null)
        {
            return AgentToolResults.Json(new
            {
                sent = false,
                error = string.IsNullOrEmpty(providerName)
                    ? "Nothing was sent: the site has no default email provider. Give provider with the name of one of the enabled providers."
                    : $"Nothing was sent: '{providerName}' is not an enabled email provider. Give provider with the name of one of the enabled providers.",
                availableProviders,
            });
        }

        var (to, toError) = await GetRecipientAsync(services, caller, arguments);

        if (toError is not null)
        {
            return toError;
        }

        var message = new MailMessage
        {
            To = to,
            Subject = Subject,
            TextBody = Body,
        };

        try
        {
            var result = await services.GetRequiredService<IEmailService>().SendAsync(message, provider.Name, cancellationToken);

            return AgentToolResults.Json(new
            {
                sent = result.Succeeded,
                provider = provider.Name,
                to,
                subject = Subject,
                errors = result.Succeeded
                    ? null
                    : result.Errors.Select(error => error.Message?.Value).Where(error => !string.IsNullOrEmpty(error)).ToList(),
                availableProviders,
                note = result.Succeeded
                    ? "The provider accepted the message. Ask the person to check that it arrived."
                    : "The provider did not send the message. Check the email settings under Settings, Communication, Email.",
            });
        }
        catch (InvalidEmailProviderException)
        {
            return AgentToolResults.Json(new
            {
                sent = false,
                error = "Nothing was sent: the selected provider is invalid or no longer enabled.",
                availableProviders,
            });
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // As on the admin page, what the provider threw is logged and not returned: it can name hosts and accounts.
            services.GetService<ILogger<SendTestEmailTool>>()?.LogError(exception, "The test email could not be sent through the provider '{Provider}'.", provider.Name);

            return AgentToolResults.Json(new
            {
                sent = false,
                provider = provider.Name,
                error = "Unable to send the message using the selected provider. The log of the site has the details.",
                availableProviders,
            });
        }
    }

    private static async Task<(string To, string Error)> GetRecipientAsync(IServiceProvider services, ClaimsPrincipal caller, AIFunctionArguments arguments)
    {
        if (arguments.TryGetFirstString("to", out var to))
        {
            to = to.Trim();

            // One address: a test is not a way to write to a list of people.
            if (to.Contains(',') || to.Contains(';') || !MailAddress.TryCreate(to, out var address) || !string.Equals(address.Address, to, StringComparison.OrdinalIgnoreCase))
            {
                return (null, "Nothing was sent: to must be one email address, for example name@example.com.");
            }

            return (to, null);
        }

        var userManager = services.GetService<UserManager<IUser>>();
        var user = userManager is null
            ? null
            : await userManager.GetUserAsync(caller);

        var email = user is null
            ? caller.FindFirstValue(ClaimTypes.Email)
            : await userManager.GetEmailAsync(user);

        if (string.IsNullOrWhiteSpace(email))
        {
            return (null, "Nothing was sent: your account has no email address. Give to with the address the test email goes to.");
        }

        return (email, null);
    }
}
