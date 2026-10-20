using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Drivers;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.Stripe.Services;

/// <summary>
/// The setup guide's card payments: the store's Stripe keys, test ones first.
/// </summary>
internal sealed class StripeSetupStep : IStoreSetupStep
{
    private readonly StripeOptions _stripeOptions;
    private readonly AdminOptions _adminOptions;

    internal readonly IStringLocalizer S;

    public StripeSetupStep(IOptions<StripeOptions> stripeOptions, IOptions<AdminOptions> adminOptions, IStringLocalizer<StripeSetupStep> stringLocalizer)
    {
        _stripeOptions = stripeOptions.Value;
        _adminOptions = adminOptions.Value;
        S = stringLocalizer;
    }

    public string Name
        => "stripe";

    public int Order
        => 30;

    public Task<StoreSetupStepDescription> DescribeAsync(CancellationToken cancellationToken = default)
    {
        // The keys in use, from the Stripe settings or the site's configuration.
        var done = _stripeOptions.IsConfigured && !string.IsNullOrEmpty(_stripeOptions.PublishableKey);

        return Task.FromResult(new StoreSetupStepDescription
        {
            Title = S["Take card payments"],
            Description = S["Customers pay by card through Stripe. Make an account at stripe.com, then copy your keys from the Stripe dashboard, under Developers, API keys, into the Stripe settings. Start with the test keys: a sale with Stripe's test card takes no real money."],
            IsDone = done,
            Status = !done ? null : _stripeOptions.IsLive ? S["Stripe is taking real payments."] : S["Stripe is in test mode: no real money is taken."],
            Url = $"~/{_adminOptions.AdminUrlPrefix}/Settings/{StripeSettingsDisplayDriver.GroupId}",
            UrlText = S["Stripe settings"],
        });
    }
}
