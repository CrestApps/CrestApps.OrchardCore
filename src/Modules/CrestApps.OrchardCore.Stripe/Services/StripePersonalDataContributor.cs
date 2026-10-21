using CrestApps.OrchardCore.Customers.PersonalData;
using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Stripe.Services;

/// <summary>
/// Exports and redacts the Stripe customers that hold a person's name, email and phone: the customers the person's
/// subscriptions and installment plans bill, and the customers the store's checkout created for the person's email.
/// </summary>
/// <remarks>
/// Redacting updates the customer through the Stripe customer API: the name becomes the replacement text, and the email
/// and phone are removed. The customer itself, its saved payment methods and its payment history stay at Stripe, so an
/// agreement that is still running keeps billing. A Stripe account can serve other systems too, so a customer found
/// only by email is redacted only when the store's checkout created it, which it marks with the checkout session in
/// the customer's metadata. Stripe failures are reported as warnings rather than stopping the rest of the request.
/// </remarks>
public sealed class StripePersonalDataContributor : ICommercePersonalDataContributor
{
    /// <summary>
    /// The metadata key the store's checkout writes on every Stripe customer it creates.
    /// </summary>
    public const string CheckoutSessionMetadataKey = "checkout_session_id";

    private readonly IOptions<StripeOptions> _options;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StripePersonalDataContributor"/> class.
    /// </summary>
    /// <param name="options">The Stripe options, which say whether Stripe is configured.</param>
    /// <param name="serviceProvider">The service provider the Stripe customer service is resolved from once Stripe is known to be configured.</param>
    /// <param name="logger">The logger.</param>
    public StripePersonalDataContributor(
        IOptions<StripeOptions> options,
        IServiceProvider serviceProvider,
        ILogger<StripePersonalDataContributor> logger)
    {
        _options = options;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task IdentifyAsync(CommercePersonalDataSubject subject, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public async Task ExportAsync(CommercePersonalDataExportContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (GetCustomerService() is not { } customerService)
        {
            return;
        }

        foreach (var customer in await GetCustomersAsync(customerService, context.Subject, context.Export.Warnings))
        {
            context.Export.ProviderCustomers.Add(new CommerceProviderCustomerRecord
            {
                ProviderKey = StripeConstants.ProcessorKey,
                CustomerReference = customer.Id,
                Name = customer.Name,
                Email = customer.Email,
                Phone = customer.Phone,
            });
        }
    }

    /// <inheritdoc/>
    public async Task AnonymizeAsync(CommercePersonalDataAnonymizeContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (GetCustomerService() is not { } customerService)
        {
            return;
        }

        foreach (var customer in await GetCustomersAsync(customerService, context.Subject, context.Result.Warnings))
        {
            // An empty string is what the Stripe API takes to remove a value; a null leaves it as it is.
            var response = await customerService.UpdateAsync(customer.Id, new UpdateCustomerRequest
            {
                Name = context.ReplacementText ?? string.Empty,
                Email = string.Empty,
                Phone = string.Empty,
            });

            if (response?.Updated == true)
            {
                context.Result.ProviderCustomers++;
            }
            else
            {
                context.Result.Warnings.Add($"The Stripe customer '{customer.Id}' could not be redacted.");
            }
        }
    }

    // Resolved only once Stripe has a secret key: the Stripe client is built from the key when it is resolved, and a
    // store without Stripe configured must still be able to answer a privacy request.
    private IStripeCustomerService GetCustomerService()
        => _options.Value.IsConfigured
            ? _serviceProvider.GetService<IStripeCustomerService>()
            : null;

    private async Task<IReadOnlyList<CustomerResponse>> GetCustomersAsync(IStripeCustomerService customerService, CommercePersonalDataSubject subject, IList<string> warnings)
    {
        var customers = new Dictionary<string, CustomerResponse>(StringComparer.Ordinal);

        var references = subject.ProviderCustomers
            .Where(customer => string.Equals(customer.ProviderKey, StripeConstants.ProcessorKey, StringComparison.OrdinalIgnoreCase))
            .Select(customer => customer.CustomerReference)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        foreach (var reference in references)
        {
            try
            {
                if (await customerService.GetAsync(reference) is { } customer)
                {
                    customers.TryAdd(customer.Id, customer);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unable to read the Stripe customer '{CustomerId}' for a personal data request.", reference);
                warnings.Add($"The Stripe customer '{reference}' could not be read.");
            }
        }

        foreach (var email in subject.Emails)
        {
            try
            {
                foreach (var customer in await customerService.ListByEmailAsync(email))
                {
                    if (customer.Metadata?.ContainsKey(CheckoutSessionMetadataKey) == true ||
                        customer.Metadata?.ContainsKey(StripeWalletService.WalletUserMetadataKey) == true)
                    {
                        customers.TryAdd(customer.Id, customer);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unable to list the Stripe customers of an email for a personal data request.");
                warnings.Add("The Stripe customers of the email could not be listed.");
            }
        }

        return [.. customers.Values];
    }
}
