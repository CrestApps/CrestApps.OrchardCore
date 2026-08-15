using CrestApps.OrchardCore.Taxation.Core;
using CrestApps.OrchardCore.Taxation.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Taxation.Fakes;

/// <summary>
/// Builds a fully wired taxation engine backed by in-memory catalog stores so tests exercise the real
/// <see cref="ITaxService"/>, calculation methods, sourcing strategies, resolvers, and providers.
/// </summary>
public sealed class TaxTestHarness
{
    private readonly IServiceProvider _services;

    /// <summary>
    /// Initializes a new instance of the <see cref="TaxTestHarness"/> class.
    /// </summary>
    /// <param name="clock">The clock used for time-based determination.</param>
    /// <param name="configure">An optional callback to register additional services or providers.</param>
    public TaxTestHarness(TestClock clock, Action<IServiceCollection> configure = null)
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddOptions();
        services.AddSingleton<IClock>(clock);

        services.AddSingleton<ITaxJurisdictionStore, InMemoryTaxJurisdictionStore>();
        services.AddSingleton<ITaxCategoryStore, InMemoryTaxCategoryStore>();
        services.AddSingleton<ITaxRuleStore, InMemoryTaxRuleStore>();
        services.AddSingleton<ITaxTableStore, InMemoryTaxTableStore>();
        services.AddSingleton<IExemptionCertificateStore, InMemoryExemptionCertificateStore>();
        services.AddSingleton<IMerchantTaxRegistrationStore, InMemoryMerchantTaxRegistrationStore>();

        services.AddTaxationCore();

        configure?.Invoke(services);

        _services = services.BuildServiceProvider();

        Clock = clock;
        Jurisdictions = _services.GetRequiredService<ITaxJurisdictionStore>();
        Categories = _services.GetRequiredService<ITaxCategoryStore>();
        Rules = _services.GetRequiredService<ITaxRuleStore>();
        Tables = _services.GetRequiredService<ITaxTableStore>();
        Exemptions = _services.GetRequiredService<IExemptionCertificateStore>();
        Registrations = _services.GetRequiredService<IMerchantTaxRegistrationStore>();
        TaxService = _services.GetRequiredService<ITaxService>();
    }

    /// <summary>
    /// Gets the clock used by the harness.
    /// </summary>
    public TestClock Clock { get; }

    /// <summary>
    /// Gets the taxation engine under test.
    /// </summary>
    public ITaxService TaxService { get; }

    /// <summary>
    /// Gets the in-memory jurisdiction store.
    /// </summary>
    public ITaxJurisdictionStore Jurisdictions { get; }

    /// <summary>
    /// Gets the in-memory category store.
    /// </summary>
    public ITaxCategoryStore Categories { get; }

    /// <summary>
    /// Gets the in-memory rule store.
    /// </summary>
    public ITaxRuleStore Rules { get; }

    /// <summary>
    /// Gets the in-memory tax table store.
    /// </summary>
    public ITaxTableStore Tables { get; }

    /// <summary>
    /// Gets the in-memory exemption certificate store.
    /// </summary>
    public IExemptionCertificateStore Exemptions { get; }

    /// <summary>
    /// Gets the in-memory merchant registration store.
    /// </summary>
    public IMerchantTaxRegistrationStore Registrations { get; }

    /// <summary>
    /// Resolves a service from the harness container.
    /// </summary>
    /// <typeparam name="TService">The service type.</typeparam>
    /// <returns>The resolved service.</returns>
    public TService GetService<TService>()
        => _services.GetRequiredService<TService>();
}
