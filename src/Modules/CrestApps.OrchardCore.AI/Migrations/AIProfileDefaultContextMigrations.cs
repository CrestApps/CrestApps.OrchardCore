using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.DependencyInjection;
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
    public static int Create()
    {
        ShellScope.AddDeferredTask(async scope =>
        {
            var profileStore = scope.ServiceProvider.GetRequiredService<IAIProfileStore>();
            var defaultOptions = scope.ServiceProvider.GetRequiredService<DefaultAIOptions>();

            var profiles = await profileStore.GetAllAsync();

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

                await profileStore.UpdateAsync(profile);
            }
        });

        return 1;
    }
}
