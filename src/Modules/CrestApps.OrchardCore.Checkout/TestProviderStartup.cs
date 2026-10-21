using CrestApps.OrchardCore.Checkout.Drivers;
using CrestApps.OrchardCore.Checkout.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Checkout;

/// <summary>
/// Registers the development test payment provider. It is registered only in the Development environment, or
/// when <see cref="TestPaymentProviderOptions.AllowOutsideDevelopment"/> is set, so turning the feature on by
/// mistake in production never lets anyone "pay" without paying.
/// </summary>
[Feature(CheckoutConstants.Features.TestProvider)]
public sealed class TestProviderStartup : StartupBase
{
    private readonly IHostEnvironment _hostEnvironment;
    private readonly IShellConfiguration _shellConfiguration;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestProviderStartup"/> class.
    /// </summary>
    /// <param name="hostEnvironment">The host environment, which decides whether the provider may be registered.</param>
    /// <param name="shellConfiguration">The tenant configuration the options are read from.</param>
    /// <param name="logger">The logger.</param>
    public TestProviderStartup(
        IHostEnvironment hostEnvironment,
        IShellConfiguration shellConfiguration,
        ILogger<TestProviderStartup> logger)
    {
        _hostEnvironment = hostEnvironment;
        _shellConfiguration = shellConfiguration;
        _logger = logger;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        var options = new TestPaymentProviderOptions
        {
            AllowOutsideDevelopment = _shellConfiguration.GetValue<bool>(TestPaymentProviderOptions.AllowOutsideDevelopmentKey),
        };

        if (!options.IsActive(_hostEnvironment))
        {
            // Nothing is registered, so the method is never offered and a request naming it is refused like any
            // unknown provider. The warning is what tells a developer why the feature they enabled shows nothing.
            _logger.LogWarning(
                "The 'Test payments' feature is enabled but inactive, because the '{EnvironmentName}' environment is not Development. Set '{SettingName}' to true to allow it outside Development.",
                _hostEnvironment.EnvironmentName,
                TestPaymentProviderOptions.AllowOutsideDevelopmentKey);

            return;
        }

        if (!_hostEnvironment.IsDevelopment())
        {
            _logger.LogWarning(
                "The 'Test payments' feature is active in the '{EnvironmentName}' environment because configuration allows it. Anyone who can open a checkout can complete it without paying.",
                _hostEnvironment.EnvironmentName);
        }

        services.AddScoped<TestCheckoutPaymentProvider>();
        services.AddScoped<ICheckoutPaymentProvider>(sp => sp.GetRequiredService<TestCheckoutPaymentProvider>());
        services.AddScoped<ICheckoutPaymentRefundProvider>(sp => sp.GetRequiredService<TestCheckoutPaymentProvider>());
        services.AddScoped<ICheckoutSavedPaymentMethodProvider>(sp => sp.GetRequiredService<TestCheckoutPaymentProvider>());
        services.AddScoped<ICheckoutRecurringPaymentProvider>(sp => sp.GetRequiredService<TestCheckoutPaymentProvider>());
        services.AddDisplayDriver<CheckoutFlowPaymentMethod, TestCheckoutPaymentMethodDisplayDriver>();
    }
}
