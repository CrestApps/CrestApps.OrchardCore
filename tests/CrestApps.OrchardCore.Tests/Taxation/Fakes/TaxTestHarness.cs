using CrestApps.Core.Services;
using CrestApps.OrchardCore.Taxation.Core;
using CrestApps.OrchardCore.Taxation.Models;
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
        services.AddLocalization();
        services.AddOptions();
        services.AddSingleton<IClock>(clock);

        services.AddTaxationCore();

        // Register the in-memory catalogs after AddTaxationCore so these closed-generic registrations take
        // precedence over the open-generic INamedCatalog<> registered by AddCatalogs().
        services.AddSingleton<INamedCatalog<TaxJurisdiction>, InMemoryNamedCatalog<TaxJurisdiction>>();
        services.AddSingleton<INamedCatalog<TaxCategory>, InMemoryNamedCatalog<TaxCategory>>();
        services.AddSingleton<INamedCatalog<TaxRule>, InMemoryNamedCatalog<TaxRule>>();
        services.AddSingleton<INamedCatalog<TaxTable>, InMemoryNamedCatalog<TaxTable>>();
        services.AddSingleton<IExemptionCertificateStore, InMemoryExemptionCertificateStore>();
        services.AddSingleton<IMerchantTaxRegistrationStore, InMemoryMerchantTaxRegistrationStore>();

        configure?.Invoke(services);

        _services = services.BuildServiceProvider();

        Clock = clock;
        Jurisdictions = _services.GetRequiredService<INamedCatalog<TaxJurisdiction>>();
        Categories = _services.GetRequiredService<INamedCatalog<TaxCategory>>();
        Rules = _services.GetRequiredService<INamedCatalog<TaxRule>>();
        Tables = _services.GetRequiredService<INamedCatalog<TaxTable>>();
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
    /// Gets the in-memory jurisdiction catalog.
    /// </summary>
    public INamedCatalog<TaxJurisdiction> Jurisdictions { get; }

    /// <summary>
    /// Gets the in-memory category catalog.
    /// </summary>
    public INamedCatalog<TaxCategory> Categories { get; }

    /// <summary>
    /// Gets the in-memory rule catalog.
    /// </summary>
    public INamedCatalog<TaxRule> Rules { get; }

    /// <summary>
    /// Gets the in-memory tax table catalog.
    /// </summary>
    public INamedCatalog<TaxTable> Tables { get; }

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
