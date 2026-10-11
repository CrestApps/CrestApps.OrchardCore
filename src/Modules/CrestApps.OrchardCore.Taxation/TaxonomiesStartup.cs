using CrestApps.OrchardCore.Taxation.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Taxation;

/// <summary>
/// Registers taxonomy-driven tax classification inheritance. When the Taxonomies feature is enabled, a
/// content item can inherit its tax classification from the taxonomy terms (product categories) it belongs
/// to, so tax codes are managed per category rather than repeated on every item.
/// </summary>
[RequireFeatures("OrchardCore.Taxonomies")]
public sealed class TaxonomiesStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ITaxClassificationProvider, TaxonomyTaxClassificationProvider>();
    }
}
