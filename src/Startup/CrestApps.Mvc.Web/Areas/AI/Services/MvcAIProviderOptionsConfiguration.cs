using CrestApps.AI.Models;
using Microsoft.Extensions.Options;

namespace CrestApps.Mvc.Web.Areas.AI.Services;


/// <summary>
/// Projects MVC-managed AI provider connections into <see cref="AIProviderOptions"/>
/// so framework AI clients can resolve connection settings from the sample app's
/// YesSql-backed admin UI.
/// </summary>
public sealed class MvcAIProviderOptionsConfiguration : IConfigureOptions<AIProviderOptions>
{
    private readonly MvcAIProviderOptionsStore _providerOptionsStore;
    private readonly ILogger<MvcAIProviderOptionsConfiguration> _logger;

    public MvcAIProviderOptionsConfiguration(
        MvcAIProviderOptionsStore providerOptionsStore,
        ILogger<MvcAIProviderOptionsConfiguration> logger)
    {
        _providerOptionsStore = providerOptionsStore;
        _logger = logger;
    }

    public void Configure(AIProviderOptions options)
    {
        try
        {
            _providerOptionsStore.ApplyTo(options);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to configure AI provider options from the MVC sample's stored AI connections.");
        }
    }
}
