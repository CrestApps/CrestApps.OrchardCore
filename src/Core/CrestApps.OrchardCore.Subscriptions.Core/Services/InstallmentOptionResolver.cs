using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using CrestApps.OrchardCore.Transactions.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Subscriptions.Core.Services;

/// <summary>
/// Collects the installment options the registered <see cref="IInstallmentOptionProvider"/> implementations offer for
/// an outstanding transaction, and keeps only the ones the plan service will accept.
/// </summary>
/// <remarks>
/// The checkout step that offers the options and the post that accepts one both ask this class, and an option is only
/// ever identified by its <see cref="InstallmentOption.Id"/>. What the customer's browser posts is matched against the
/// options read again on the server, so a tampered form can choose only an option that is really offered.
/// </remarks>
public sealed class InstallmentOptionResolver
{
    private const int MaxInstallments = 120;
    private const int MaxOptionIdLength = 64;

    private readonly IEnumerable<IInstallmentOptionProvider> _providers;
    private readonly ICheckoutPaymentProviderResolver _providerResolver;
    private readonly IEnumerable<ICheckoutSavedPaymentMethodProvider> _savedPaymentMethodProviders;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="InstallmentOptionResolver"/> class.
    /// </summary>
    /// <param name="providers">The registered option providers.</param>
    /// <param name="providerResolver">Resolves the payment providers, to tell whether one can keep a card.</param>
    /// <param name="savedPaymentMethodProviders">The providers that read back a kept card.</param>
    /// <param name="logger">The logger.</param>
    public InstallmentOptionResolver(
        IEnumerable<IInstallmentOptionProvider> providers,
        ICheckoutPaymentProviderResolver providerResolver,
        IEnumerable<ICheckoutSavedPaymentMethodProvider> savedPaymentMethodProviders,
        ILogger<InstallmentOptionResolver> logger)
    {
        _providers = providers;
        _providerResolver = providerResolver;
        _savedPaymentMethodProviders = savedPaymentMethodProviders;
        _logger = logger;
    }

    /// <summary>
    /// Returns the options offered for the transaction, in the order the providers offered them, or none when the
    /// transaction can't be paid in installments or no payment provider can keep the card.
    /// </summary>
    /// <param name="transaction">The outstanding transaction.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public async Task<IReadOnlyList<InstallmentOption>> GetOptionsAsync(Transaction transaction, CancellationToken cancellationToken = default)
    {
        if (!InstallmentTransactionEligibility.CanSplit(transaction) || !CanKeepCards())
        {
            return [];
        }

        var options = new List<InstallmentOption>();
        var ids = new HashSet<string>(StringComparer.Ordinal);

        foreach (var provider in _providers)
        {
            if (!string.IsNullOrEmpty(provider.Source) && !string.Equals(provider.Source, transaction.Source, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            IEnumerable<InstallmentOption> offered;

            try
            {
                offered = await provider.GetOptionsAsync(transaction, cancellationToken);
            }
            catch (Exception ex)
            {
                // One provider's failure must not keep the customer from paying, in full or with another provider's
                // options.
                _logger.LogError(ex, "The installment option provider '{Provider}' failed for transaction '{TransactionId}'.", provider.GetType().FullName, transaction.ItemId);

                continue;
            }

            foreach (var option in offered ?? [])
            {
                if (IsValid(option, transaction) && ids.Add(option.Id))
                {
                    options.Add(option);
                }
            }
        }

        return options;
    }

    /// <summary>
    /// Returns the option with the supplied identifier among those offered for the transaction, or
    /// <see langword="null"/> when it is not offered.
    /// </summary>
    /// <param name="transaction">The outstanding transaction.</param>
    /// <param name="optionId">The identifier the customer chose.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public async Task<InstallmentOption> FindOptionAsync(Transaction transaction, string optionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(optionId))
        {
            return null;
        }

        var options = await GetOptionsAsync(transaction, cancellationToken);

        return options.FirstOrDefault(option => string.Equals(option.Id, optionId, StringComparison.Ordinal));
    }

    /// <summary>
    /// Returns <see langword="true"/> when the option makes a plan the service accepts for the transaction: a number of
    /// payments it allows, a down payment below the balance, and payments that are each more than nothing.
    /// </summary>
    /// <param name="option">The option.</param>
    /// <param name="transaction">The outstanding transaction.</param>
    public static bool IsValid(InstallmentOption option, Transaction transaction)
    {
        if (option is null ||
            transaction is null ||
            string.IsNullOrWhiteSpace(option.Id) ||
            option.Id.Length > MaxOptionIdLength ||
            string.Equals(option.Id, SubscriptionConstants.InstallmentPlans.PayInFullOptionId, StringComparison.Ordinal) ||
            option.InstallmentCount < 1 ||
            option.InstallmentCount > MaxInstallments ||
            !Enum.IsDefined(option.Frequency) ||
            option.DownPaymentAmount < 0m ||
            option.DownPaymentAmount >= transaction.OutstandingAmount)
        {
            return false;
        }

        var amounts = InstallmentScheduleCalculator.SplitAmounts(transaction.OutstandingAmount, option.DownPaymentAmount, option.InstallmentCount, transaction.Currency);

        return amounts.Count == option.InstallmentCount && amounts.All(amount => amount > 0m);
    }

    // The same test the plan service applies: the card is entered on the checkout page and read back later.
    private bool CanKeepCards()
        => _providerResolver.GetProviders().Any(provider =>
            provider.Capabilities.SupportsSavedPaymentMethods &&
            provider.Capabilities.SupportsEmbeddedElements &&
            _savedPaymentMethodProviders.Any(saved => string.Equals(saved.Key, provider.Key, StringComparison.OrdinalIgnoreCase)));
}
