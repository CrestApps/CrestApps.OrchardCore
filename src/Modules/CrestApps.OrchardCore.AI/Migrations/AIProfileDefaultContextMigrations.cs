using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Data.Migration;
using OrchardCore.Entities;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.AI.Migrations;

/// <summary>
/// We no longer fall back to the default AI options at runtime for missing values in the profile metadata.
/// Instead, we apply the suggested default values when a new profile is created. To ensure backward compatibility,
/// we update all existing profiles to include these default values where they are missing.
/// </summary>
internal sealed class AIProfileDefaultContextMigrations : DataMigration
{
#pragma warning disable CA1822 // Mark members as static
    public int Create()
#pragma warning restore CA1822 // Mark members as static
    {
        ShellScope.AddDeferredTask(async scope =>
        {
            var profileCatalog = scope.ServiceProvider.GetRequiredService<INamedSourceCatalog<AIProfile>>();
            var defaultOptions = scope.ServiceProvider.GetRequiredService<IOptions<DefaultAIOptions>>().Value;

            var profiles = await profileCatalog.GetAllAsync();

            foreach (var profile in profiles)
            {
                profile.Alter<AIProfileMetadata>(metadata =>
                {
                    if (!metadata.MaxTokens.HasValue)
                    {
                        // Previously, we used 800 as the default max tokens if not specified.
                        // We'll use 800 for backward compatibility while new profile will have no value set.
                        metadata.MaxTokens = defaultOptions.MaxOutputTokens ?? 800;
                    }

                    if (!metadata.PresencePenalty.HasValue)
                    {
                        metadata.PresencePenalty = defaultOptions.PresencePenalty;
                    }

                    if (!metadata.FrequencyPenalty.HasValue)
                    {
                        metadata.FrequencyPenalty = defaultOptions.FrequencyPenalty;
                    }

                    if (!metadata.Temperature.HasValue)
                    {
                        metadata.Temperature = defaultOptions.Temperature;
                    }

                    if (!metadata.TopP.HasValue)
                    {
                        metadata.TopP = defaultOptions.TopP;
                    }

                    if (!metadata.PastMessagesCount.HasValue)
                    {
                        metadata.PastMessagesCount = defaultOptions.PastMessagesCount;
                    }
                });

                await profileCatalog.UpdateAsync(profile);
            }
        });

        return 1;
    }
}
