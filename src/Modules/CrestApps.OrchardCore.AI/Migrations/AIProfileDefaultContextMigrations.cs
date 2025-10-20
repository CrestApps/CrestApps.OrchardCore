using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.Options;
using OrchardCore.Data.Migration;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.Migrations;

/// <summary>
/// We no longer fall back to the default AI options at runtime for missing values in the profile metadata.
/// Instead, we apply the suggested default values when a new profile is created. To ensure backward compatibility,
/// we update all existing profiles to include these default values where they are missing.
/// </summary>
internal sealed class AIProfileDefaultContextMigrations : DataMigration
{
    private readonly INamedSourceCatalog<AIProfile> _profileCatalog;
    private readonly DefaultAIOptions _defaultOptions;

    public AIProfileDefaultContextMigrations(
        INamedSourceCatalog<AIProfile> profileCatalog,
        IOptions<DefaultAIOptions> defaultOptions)
    {
        _profileCatalog = profileCatalog;
        _defaultOptions = defaultOptions.Value;
    }

    public async Task<int> CreateAsync()
    {
        var profiles = await _profileCatalog.GetAllAsync();

        foreach (var profile in profiles)
        {
            profile.Alter<AIProfileMetadata>(metadata =>
            {
                if (!metadata.MaxTokens.HasValue)
                {
                    // Previously, we used 800 as the default max tokens if not specified.
                    // We'll use 800 for backward compatibility while new profile will have no value set.
                    metadata.MaxTokens = _defaultOptions.MaxOutputTokens ?? 800;
                }

                if (!metadata.PresencePenalty.HasValue)
                {
                    metadata.PresencePenalty = _defaultOptions.PresencePenalty;
                }

                if (!metadata.FrequencyPenalty.HasValue)
                {
                    metadata.FrequencyPenalty = _defaultOptions.FrequencyPenalty;
                }

                if (!metadata.Temperature.HasValue)
                {
                    metadata.Temperature = _defaultOptions.Temperature;
                }

                if (!metadata.TopP.HasValue)
                {
                    metadata.TopP = _defaultOptions.TopP;
                }

                if (!metadata.PastMessagesCount.HasValue)
                {
                    metadata.PastMessagesCount = _defaultOptions.PastMessagesCount;
                }
            });

            await _profileCatalog.UpdateAsync(profile);
        }

        return 1;
    }
}
